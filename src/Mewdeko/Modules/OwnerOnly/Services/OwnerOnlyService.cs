﻿using System.Collections.Immutable;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;
using Mewdeko.Common.Configs;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Services.Settings;
using Mewdeko.Services.strings;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Octokit;
using OpenAI_API;
using OpenAI_API.Chat;
using OpenAI_API.Models;
using Serilog;
using StackExchange.Redis;
using Embed = Discord.Embed;
using Image = Discord.Image;

namespace Mewdeko.Modules.OwnerOnly.Services;

public class OwnerOnlyService : ILateExecutor, IReadyExecutor, INService
{
    private readonly Mewdeko bot;
    private readonly BotConfigService bss;

    private readonly IDataCache cache;
    private int currentStatusNum;
    private readonly DiscordSocketClient client;
    private readonly CommandHandler cmdHandler;
    private readonly IBotCredentials creds;
    private readonly DbService db;
    private readonly IHttpClientFactory httpFactory;
    private readonly Replacer rep;
    private readonly IBotStrings strings;
    private readonly GuildSettingsService guildSettings;
    private readonly ConcurrentDictionary<ulong, Conversation> conversations = new();

#pragma warning disable CS8714
    private ConcurrentDictionary<ulong?, ConcurrentDictionary<int, Timer>> autoCommands =
#pragma warning restore CS8714
        new();

    private ImmutableDictionary<ulong, IDMChannel> ownerChannels =
        new Dictionary<ulong, IDMChannel>().ToImmutableDictionary();

    public OwnerOnlyService(DiscordSocketClient client, CommandHandler cmdHandler, DbService db,
        IBotStrings strings, IBotCredentials creds, IDataCache cache, IHttpClientFactory factory,
        BotConfigService bss, IEnumerable<IPlaceholderProvider> phProviders, Mewdeko bot,
        GuildSettingsService guildSettings, EventHandler handler)
    {
        var redis = cache.Redis;
        this.cmdHandler = cmdHandler;
        this.db = db;
        this.strings = strings;
        this.client = client;
        this.creds = creds;
        this.cache = cache;
        this.bot = bot;
        this.guildSettings = guildSettings;
        var imgs = cache.LocalImages;
        httpFactory = factory;
        this.bss = bss;
        handler.MessageReceived += OnMessageReceived;
        if (client.ShardId == 0)
        {
            rep = new ReplacementBuilder()
                .WithClient(client)
                .WithProviders(phProviders)
                .Build();

            _ = Task.Run(RotatingStatuses);
        }

        var sub = redis.GetSubscriber();
        if (this.client.ShardId == 0)
        {
            sub.Subscribe($"{this.creds.RedisKey()}_reload_images",
                delegate { imgs.Reload(); }, CommandFlags.FireAndForget);
        }

        sub.Subscribe($"{this.creds.RedisKey()}_leave_guild", async (_, v) =>
        {
            try
            {
                var guildStr = v.ToString()?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(guildStr))
                    return;
                var server = this.client.Guilds.FirstOrDefault(g => g.Id.ToString() == guildStr) ??
                             this.client.Guilds.FirstOrDefault(g => g.Name.Trim().ToUpperInvariant() == guildStr);

                if (server == null) return;

                if (server.OwnerId != this.client.CurrentUser.Id)
                {
                    await server.LeaveAsync().ConfigureAwait(false);
                    Log.Information("Left server {ServerName} [{ServerId}]", server.Name, server.Id);
                }
                else
                {
                    await server.DeleteAsync().ConfigureAwait(false);
                    Log.Information("Deleted server {ServerName} [{ServerId}]", server.Name, server.Id);
                }
            }
            catch
            {
                // ignored
            }
        }, CommandFlags.FireAndForget);

        _ = CheckUpdateTimer();
        handler.GuildMemberUpdated += QuarantineCheck;
    }

    private async Task QuarantineCheck(Cacheable<SocketGuildUser, ulong> args, SocketGuildUser arsg2)
    {
        if (!args.HasValue)
            return;

        if (args.Id != client.CurrentUser.Id)
            return;

        var value = args.Value;

        if (value.Roles is null)
            return;


        if (!bss.Data.QuarantineNotification)
            return;

        if (!Equals(value.Roles, arsg2.Roles))
        {
            var quarantineRole = value.Guild.Roles.FirstOrDefault(x => x.Name == "Quarantine");
            if (quarantineRole is null)
                return;

            if (value.Roles.All(x => x.Id != quarantineRole.Id) && arsg2.Roles.Any(x => x.Id == quarantineRole.Id))
            {
                if (bss.Data.ForwardToAllOwners)
                {
                    foreach (var i in creds.OwnerIds)
                    {
                        var user = await client.Rest.GetUserAsync(i);
                        if (user is null) continue;
                        var channel = await user.CreateDMChannelAsync();
                        await channel.SendMessageAsync(
                            $"Quarantined in {value.Guild.Name} [{value.Guild.Id}]");
                    }
                }
                else
                {
                    var user = await client.Rest.GetUserAsync(creds.OwnerIds[0]);
                    if (user is not null)
                    {
                        var channel = await user.CreateDMChannelAsync();
                        await channel.SendMessageAsync(
                            $"Quarantined in {value.Guild.Name} [{value.Guild.Id}]");
                    }
                }
            }
        }
    }

    private async Task CheckUpdateTimer()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(bss.Data.CheckUpdateInterval));
        do
        {
            var github = new GitHubClient(new ProductHeaderValue("Mewdeko"));
            var redis = cache.Redis.GetDatabase();
            switch (bss.Data.CheckForUpdates)
            {
                case UpdateCheckType.Release:
                    var latestRelease = await github.Repository.Release.GetLatest("SylveonDeko", "Mewdeko");
                    var eb = new EmbedBuilder()
                        .WithAuthor($"New Release found: {latestRelease.TagName}",
                            "https://seeklogo.com/images/G/github-logo-5F384D0265-seeklogo.com.png",
                            latestRelease.HtmlUrl)
                        .WithDescription(
                            $"- If on Windows, you can download the new release [here]({latestRelease.Assets[0].BrowserDownloadUrl})\n" +
                            $"- If running source just run the `{bss.Data.Prefix}update command and the bot will do the rest for you.`")
                        .WithOkColor();
                    var list = await redis.StringGetAsync($"{creds.RedisKey()}_ReleaseList");
                    if (!list.HasValue)
                    {
                        await redis.StringSetAsync($"{creds.RedisKey()}_ReleaseList",
                            JsonConvert.SerializeObject(latestRelease));
                        Log.Information("Setting latest release to {ReleaseTag}", latestRelease.TagName);
                    }
                    else
                    {
                        var release = JsonConvert.DeserializeObject<Release>(list);
                        if (release.TagName != latestRelease.TagName)
                        {
                            Log.Information("New release found: {ReleaseTag}", latestRelease.TagName);
                            await redis.StringSetAsync($"{creds.RedisKey()}_ReleaseList",
                                JsonConvert.SerializeObject(latestRelease));
                            if (bss.Data.ForwardToAllOwners)
                            {
                                foreach (var i in creds.OwnerIds)
                                {
                                    var user = await client.Rest.GetUserAsync(i);
                                    if (user is null) continue;
                                    var channel = await user.CreateDMChannelAsync();
                                    await channel.SendMessageAsync(embed: eb.Build());
                                }
                            }
                            else
                            {
                                var user = await client.Rest.GetUserAsync(creds.OwnerIds[0]);
                                if (user is not null)
                                {
                                    var channel = await user.CreateDMChannelAsync();
                                    await channel.SendMessageAsync(embed: eb.Build());
                                }
                            }
                        }
                    }

                    break;
                case UpdateCheckType.Commit:
                    var latestCommit =
                        await github.Repository.Commit.Get("SylveonDeko", "Mewdeko", bss.Data.UpdateBranch);
                    if (latestCommit is null)
                    {
                        Log.Warning(
                            "Failed to get latest commit, make sure you have the correct branch set in bot.yml");
                        break;
                    }

                    var redisCommit = await redis.StringGetAsync($"{creds.RedisKey()}_CommitList");
                    if (!redisCommit.HasValue)
                    {
                        await redis.StringSetAsync($"{creds.RedisKey()}_CommitList",
                            latestCommit.Sha);
                        Log.Information("Setting latest commit to {CommitSha}", latestCommit.Sha);
                    }
                    else
                    {
                        if (redisCommit.ToString() != latestCommit.Sha)
                        {
                            Log.Information("New commit found: {CommitSha}", latestCommit.Sha);
                            await redis.StringSetAsync($"{creds.RedisKey()}_CommitList",
                                latestCommit.Sha);
                            if (bss.Data.ForwardToAllOwners)
                            {
                                foreach (var i in creds.OwnerIds)
                                {
                                    var user = await client.Rest.GetUserAsync(i);
                                    if (user is null) continue;
                                    var channel = await user.CreateDMChannelAsync();
                                    await channel.SendMessageAsync(
                                        $"New commit found: {latestCommit.Sha}\n{latestCommit.HtmlUrl}");
                                }
                            }
                            else
                            {
                                var user = await client.Rest.GetUserAsync(creds.OwnerIds[0]);
                                if (user is not null)
                                {
                                    var channel = await user.CreateDMChannelAsync();
                                    await channel.SendMessageAsync(
                                        $"New commit found: {latestCommit.Sha}\n{latestCommit.HtmlUrl}");
                                }
                            }
                        }
                    }

                    break;
                case UpdateCheckType.None:
                    break;
                default:
                    Log.Error("Invalid UpdateCheckType {UpdateCheckType}", bss.Data.CheckForUpdates);
                    break;
            }
        } while (await timer.WaitForNextTickAsync());
    }

    private async Task OnMessageReceived(SocketMessage args)
    {
        if (args.Channel is not IGuildChannel guildChannel)
            return;
        var prefix = await guildSettings.GetPrefix(guildChannel.GuildId);
        if (args.Content.StartsWith(prefix))
            return;
        if (bss.Data.ChatGptKey is null or "" || bss.Data.ChatGptChannel is 0)
            return;
        if (args.Author.IsBot)
            return;
        if (args.Channel.Id != bss.Data.ChatGptChannel)
            return;
        if (args is not IUserMessage usrMsg)
            return;
        try
        {
            var api = new OpenAIAPI(bss.Data.ChatGptKey);
            if (args.Content is "deletesession")
            {
                if (conversations.TryRemove(args.Author.Id, out _))
                {
                    await usrMsg.SendConfirmReplyAsync("Session deleted");
                    return;
                }

                await usrMsg.SendConfirmReplyAsync("No session to delete");
                return;
            }

            await using var uow = db.GetDbContext();
            (Database.Models.OwnerOnly actualItem, bool added) toUpdate = uow.OwnerOnly.Any()
                ? (await uow.OwnerOnly.FirstOrDefaultAsync(), false)
                : (new Database.Models.OwnerOnly
                {
                    GptTokensUsed = 0
                }, true);

            if (!conversations.TryGetValue(args.Author.Id, out var conversation))
            {
                conversation = StartNewConversation(args.Author, api);
                conversations.TryAdd(args.Author.Id, conversation);
            }

            conversation.AppendUserInput(args.Content);

            var loadingMsg = await usrMsg.Channel.SendConfirmAsync($"{bss.Data.LoadingEmote} Awaiting response...");
            await StreamResponseAndUpdateEmbedAsync(conversation, loadingMsg, uow, toUpdate, args.Author);
        }
        catch (Exception e)
        {
            Log.Warning(e, "Error in ChatGPT");
            await usrMsg.SendErrorReplyAsync("Something went wrong, please try again later.");
        }
    }

    private Conversation StartNewConversation(SocketUser user, IOpenAIAPI api)
    {
        var modelToUse = bss.Data.ChatGptModel switch
        {
            "gpt-4-0613" => Model.GPT4_32k_Context,
            "gpt4" or "gpt-4" => Model.GPT4,
            _ => Model.ChatGPTTurbo
        };

        var chat = api.Chat.CreateConversation(new ChatRequest
        {
            MaxTokens = bss.Data.ChatGptMaxTokens, Temperature = bss.Data.ChatGptTemperature, Model = modelToUse
        });
        chat.AppendSystemMessage(bss.Data.ChatGptInitPrompt);
        chat.AppendSystemMessage($"The user's name is {user}.");
        return chat;
    }

    private static async Task StreamResponseAndUpdateEmbedAsync(Conversation conversation, IUserMessage loadingMsg,
        MewdekoContext uow, (Database.Models.OwnerOnly actualItem, bool added) toUpdate, SocketUser author)
    {
        var responseBuilder = new StringBuilder();
        var lastUpdate = DateTimeOffset.UtcNow;

        await conversation.StreamResponseFromChatbotAsync(async partialResponse =>
        {
            responseBuilder.Append(partialResponse);
            if (!((DateTimeOffset.UtcNow - lastUpdate).TotalSeconds >= 1)) return;
            lastUpdate = DateTimeOffset.UtcNow;
            var embeds = BuildEmbeds(responseBuilder.ToString(), author, toUpdate.actualItem.GptTokensUsed,
                conversation);
            await loadingMsg.ModifyAsync(m => m.Embeds = embeds.ToArray());
        });

        var finalResponse = responseBuilder.ToString();
        if (conversation.MostResentAPIResult.Usage != null)
        {
            toUpdate.actualItem.GptTokensUsed += conversation.MostResentAPIResult.Usage.TotalTokens;
        }

        if (toUpdate.added)
            uow.OwnerOnly.Add(toUpdate.actualItem);
        else
            uow.OwnerOnly.Update(toUpdate.actualItem);
        await uow.SaveChangesAsync();

        var finalEmbeds = BuildEmbeds(finalResponse, author, toUpdate.actualItem.GptTokensUsed, conversation);
        await loadingMsg.ModifyAsync(m => m.Embeds = finalEmbeds.ToArray());
    }

    private static List<Embed> BuildEmbeds(string response, IUser requester, int totalTokensUsed,
        Conversation conversation)
    {
        var embeds = new List<Embed>();
        var partIndex = 0;
        while (partIndex < response.Length)
        {
            var length = Math.Min(4096, response.Length - partIndex);
            var description = response.Substring(partIndex, length);
            var embedBuilder = new EmbedBuilder()
                .WithDescription(description)
                .WithOkColor();

            if (partIndex == 0)
                embedBuilder.WithAuthor("ChatGPT",
                    "https://seeklogo.com/images/C/chatgpt-logo-02AFA704B5-seeklogo.com.png");

            if (partIndex + length == response.Length)
                embedBuilder.WithFooter(
                    $"Requested by {requester.Username} | Response Tokens: {conversation.MostResentAPIResult.Usage?.TotalTokens} | Total Used: {totalTokensUsed}");

            embeds.Add(embedBuilder.Build());
            partIndex += length;
        }

        return embeds;
    }


    public async Task ClearUsedTokens()
    {
        await using var uow = db.GetDbContext();
        var val = await uow.OwnerOnly.FirstOrDefaultAsync();
        if (val is null)
            return;
        val.GptTokensUsed = 0;
        uow.OwnerOnly.Update(val);
        await uow.SaveChangesAsync();
    }

    // forwards dms
    public async Task LateExecute(DiscordSocketClient discordSocketClient, IGuild guild, IUserMessage msg)
    {
        var bs = bss.Data;
        if (msg.Channel is IDMChannel && bss.Data.ForwardMessages && ownerChannels.Count > 0)
        {
            var title = $"{strings.GetText("dm_from")} [{msg.Author}]({msg.Author.Id})";

            var attachamentsTxt = strings.GetText("attachments");

            var toSend = msg.Content;

            if (msg.Attachments.Count > 0)
            {
                toSend +=
                    $"\n\n{Format.Code(attachamentsTxt)}:\n{string.Join("\n", msg.Attachments.Select(a => a.ProxyUrl))}";
            }

            if (bs.ForwardToAllOwners)
            {
                var allOwnerChannels = ownerChannels.Values;

                foreach (var ownerCh in allOwnerChannels.Where(ch => ch.Recipient.Id != msg.Author.Id))
                {
                    try
                    {
                        await ownerCh.SendConfirmAsync(title, toSend).ConfigureAwait(false);
                    }
                    catch
                    {
                        Log.Warning("Can't contact owner with id {0}", ownerCh.Recipient.Id);
                    }
                }
            }
            else
            {
                var firstOwnerChannel = ownerChannels.Values.First();
                if (firstOwnerChannel.Recipient.Id != msg.Author.Id)
                {
                    try
                    {
                        await firstOwnerChannel.SendConfirmAsync(title, toSend).ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }
    }

    public async Task OnReadyAsync()
    {
        await using var uow = db.GetDbContext();

        autoCommands =
            uow.AutoCommands
                .AsNoTracking()
                .Where(x => x.Interval >= 5)
                .AsEnumerable()
                .GroupBy(x => x.GuildId)
                .ToDictionary(x => x.Key,
                    y => y.ToDictionary(x => x.Id, TimerFromAutoCommand)
                        .ToConcurrent())
                .ToConcurrent();

        foreach (var cmd in uow.AutoCommands.AsNoTracking().Where(x => x.Interval == 0))
        {
            try
            {
                await ExecuteCommand(cmd).ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
        }

        if (client.ShardId == 0)
        {
            var channels = await Task.WhenAll(creds.OwnerIds.Select(id =>
            {
                var user = client.GetUser(id);
                return user == null ? Task.FromResult<IDMChannel?>(null) : user.CreateDMChannelAsync();
            })).ConfigureAwait(false);

            ownerChannels = channels.Where(x => x is not null)
                .ToDictionary(x => x.Recipient.Id, x => x)
                .ToImmutableDictionary();

            if (ownerChannels.Count == 0)
            {
                Log.Warning(
                    "No owner channels created! Make sure you've specified the correct OwnerId in the credentials.json file and invited the bot to a Discord server");
            }
            else
            {
                Log.Information(
                    $"Created {ownerChannels.Count} out of {creds.OwnerIds.Length} owner message channels.");
            }
        }
    }

    private async Task RotatingStatuses()
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync())
        {
            try
            {
                if (!bss.Data.RotateStatuses) continue;

                IReadOnlyList<RotatingPlayingStatus> rotatingStatuses;
                var uow = db.GetDbContext();
                await using (uow.ConfigureAwait(false))
                {
                    rotatingStatuses = uow.RotatingStatus.AsNoTracking().OrderBy(x => x.Id).ToList();
                }

                if (rotatingStatuses.Count == 0)
                    continue;

                var playingStatus = currentStatusNum >= rotatingStatuses.Count
                    ? rotatingStatuses[currentStatusNum = 0]
                    : rotatingStatuses[currentStatusNum++];

                var statusText = rep.Replace(playingStatus.Status);
                await bot.SetGameAsync(statusText, playingStatus.Type).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Rotating playing status errored: {ErrorMessage}", ex.Message);
            }
        }
    }

    public async Task<string?> RemovePlayingAsync(int index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));

        await using var uow = db.GetDbContext();
        var toRemove = await uow.RotatingStatus
            .AsQueryable()
            .AsNoTracking()
            .Skip(index)
            .FirstOrDefaultAsync().ConfigureAwait(false);

        if (toRemove is null)
            return null;

        uow.Remove(toRemove);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        return toRemove.Status;
    }

    public async Task AddPlaying(ActivityType t, string status)
    {
        await using var uow = db.GetDbContext();
        var toAdd = new RotatingPlayingStatus
        {
            Status = status, Type = t
        };
        uow.Add(toAdd);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public bool ToggleRotatePlaying()
    {
        var enabled = false;
        bss.ModifyConfig(bs => enabled = bs.RotateStatuses = !bs.RotateStatuses);
        return enabled;
    }

    public IReadOnlyList<RotatingPlayingStatus> GetRotatingStatuses()
    {
        using var uow = db.GetDbContext();
        return uow.RotatingStatus.AsNoTracking().ToList();
    }

    private Timer TimerFromAutoCommand(AutoCommand x) =>
        new(async obj => await ExecuteCommand((AutoCommand)obj).ConfigureAwait(false),
            x,
            x.Interval * 1000,
            x.Interval * 1000);

    private async Task ExecuteCommand(AutoCommand cmd)
    {
        try
        {
            if (cmd.GuildId is null)
                return;
            var guildShard = (int)((cmd.GuildId.Value >> 22) % (ulong)creds.TotalShards);
            if (guildShard != client.ShardId)
                return;
            var prefix = await guildSettings.GetPrefix(cmd.GuildId.Value);
            //if someone already has .die as their startup command, ignore it
            if (cmd.CommandText.StartsWith($"{prefix}die", StringComparison.InvariantCulture))
                return;
            await cmdHandler.ExecuteExternal(cmd.GuildId, cmd.ChannelId, cmd.CommandText).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error in SelfService ExecuteCommand");
        }
    }

    public void AddNewAutoCommand(AutoCommand cmd)
    {
        using (var uow = db.GetDbContext())
        {
            uow.AutoCommands.Add(cmd);
            uow.SaveChanges();
        }

        if (cmd.Interval >= 5)
        {
            var autos = autoCommands.GetOrAdd(cmd.GuildId, new ConcurrentDictionary<int, Timer>());
            autos.AddOrUpdate(cmd.Id, _ => TimerFromAutoCommand(cmd), (_, old) =>
            {
                old.Change(Timeout.Infinite, Timeout.Infinite);
                return TimerFromAutoCommand(cmd);
            });
        }
    }

    public string SetDefaultPrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentNullException(nameof(prefix));

        bss.ModifyConfig(bs => bs.Prefix = prefix);

        return prefix;
    }

    public IEnumerable<AutoCommand> GetStartupCommands()
    {
        using var uow = db.GetDbContext();
        return
            uow.AutoCommands
                .AsNoTracking()
                .Where(x => x.Interval == 0)
                .OrderBy(x => x.Id)
                .ToList();
    }

    public IEnumerable<AutoCommand> GetAutoCommands()
    {
        using var uow = db.GetDbContext();
        return
            uow.AutoCommands
                .AsNoTracking()
                .Where(x => x.Interval >= 5)
                .OrderBy(x => x.Id)
                .ToList();
    }

    public Task LeaveGuild(string guildStr)
    {
        var sub = cache.Redis.GetSubscriber();
        return sub.PublishAsync($"{creds.RedisKey()}_leave_guild", guildStr);
    }

    public bool RestartBot()
    {
        var cmd = creds.RestartCommand;
        if (string.IsNullOrWhiteSpace(cmd.Cmd)) return false;

        Restart();
        return true;
    }

    public bool RemoveStartupCommand(int index, out AutoCommand cmd)
    {
        using var uow = db.GetDbContext();
        cmd = uow.AutoCommands
            .AsNoTracking()
            .Where(x => x.Interval == 0)
            .Skip(index)
            .FirstOrDefault();

        if (cmd != null)
        {
            uow.Remove(cmd);
            uow.SaveChanges();
            return true;
        }

        return false;
    }

    public bool RemoveAutoCommand(int index, out AutoCommand cmd)
    {
        using var uow = db.GetDbContext();
        cmd = uow.AutoCommands
            .AsNoTracking()
            .Where(x => x.Interval >= 5)
            .Skip(index)
            .FirstOrDefault();

        if (cmd == null) return false;
        uow.Remove(cmd);
        if (autoCommands.TryGetValue(cmd.GuildId, out var autos))
        {
            if (autos.TryRemove(cmd.Id, out var timer))
                timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        uow.SaveChanges();
        return true;
    }

    public async Task<bool> SetAvatar(string img)
    {
        if (string.IsNullOrWhiteSpace(img))
            return false;

        if (!Uri.IsWellFormedUriString(img, UriKind.Absolute))
            return false;

        var uri = new Uri(img);

        using var http = httpFactory.CreateClient();
        using var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        if (!sr.IsImage())
            return false;

        // i can't just do ReadAsStreamAsync because dicord.net's image poops itself
        var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        var imgStream = imgData.ToStream();
        await using var _ = imgStream.ConfigureAwait(false);
        await client.CurrentUser.ModifyAsync(u => u.Avatar = new Image(imgStream)).ConfigureAwait(false);

        return true;
    }

    public void ClearStartupCommands()
    {
        using var uow = db.GetDbContext();
        var toRemove =
            uow.AutoCommands
                .AsNoTracking()
                .Where(x => x.Interval == 0);

        uow.AutoCommands.RemoveRange(toRemove);
        uow.SaveChanges();
    }

    public void ReloadImages()
    {
        var sub = cache.Redis.GetSubscriber();
        sub.Publish($"{creds.RedisKey()}_reload_images", "");
    }

    public void Die()
    {
        var sub = cache.Redis.GetSubscriber();
        sub.Publish($"{creds.RedisKey()}_die", "", CommandFlags.FireAndForget);
    }

    public void Restart()
    {
        Process.Start(creds.RestartCommand.Cmd, creds.RestartCommand.Args);
        var sub = cache.Redis.GetSubscriber();
        sub.Publish($"{creds.RedisKey()}_die", "", CommandFlags.FireAndForget);
    }

    public bool RestartShard(int shardId)
    {
        if (shardId < 0 || shardId >= creds.TotalShards)
            return false;

        var pub = cache.Redis.GetSubscriber();
        pub.Publish($"{creds.RedisKey()}_shardcoord_stop",
            JsonConvert.SerializeObject(shardId),
            CommandFlags.FireAndForget);

        return true;
    }

    public bool ForwardMessages()
    {
        var isForwarding = false;
        bss.ModifyConfig(config => isForwarding = config.ForwardMessages = !config.ForwardMessages);

        return isForwarding;
    }

    public bool ForwardToAll()
    {
        var isToAll = false;
        bss.ModifyConfig(config => isToAll = config.ForwardToAllOwners = !config.ForwardToAllOwners);
        return isToAll;
    }
}
﻿using System.Collections.Immutable;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Services.Settings;
using Mewdeko.Services.strings;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using OpenAI_API;
using OpenAI_API.Chat;
using OpenAI_API.Models;
using Serilog;
using StackExchange.Redis;
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

            _ = Task.Run(async () => await RotatingStatuses());
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
                    Log.Information($"Left server {server.Name} [{server.Id}]");
                }
                else
                {
                    await server.DeleteAsync().ConfigureAwait(false);
                    Log.Information($"Deleted server {server.Name} [{server.Id}]");
                }
            }
            catch
            {
                // ignored
            }
        }, CommandFlags.FireAndForget);
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
        var api = new OpenAIAPI(bss.Data.ChatGptKey);
        if (args is not IUserMessage usrMsg)
            return;
        if (args.Content is "deletesession")
            if (conversations.TryRemove(args.Author.Id, out _))
            {
                await usrMsg.SendConfirmReplyAsync("Session deleted");
                return;
            }
            else
            {
                await usrMsg.SendConfirmReplyAsync("No session to delete");
                return;
            }

        await using var uow = db.GetDbContext();
        (Database.Models.OwnerOnly actualItem, bool added) toUpdate;
        if (uow.OwnerOnly.Any())
        {
            toUpdate = (await uow.OwnerOnly.FirstOrDefaultAsync(), false);
        }
        else
        {
            toUpdate = (new Database.Models.OwnerOnly
            {
                GptTokensUsed = 0
            }, true);
        }

        if (conversations.TryGetValue(args.Author.Id, out var conversation))
        {
            conversation.AppendUserInput(args.Content);
            if (!string.IsNullOrEmpty(bss.Data.ChatGptWebhook))
            {
                try
                {
                    var webhook = new DiscordWebhookClient(bss.Data.ChatGptWebhook);
                    var msg = await webhook.SendConfirmAsync($"{bss.Data.LoadingEmote} awaiting response...");
                    var response = await conversation.GetResponseFromChatbotAsync();
                    toUpdate.actualItem.GptTokensUsed += conversation.MostResentAPIResult.Usage.TotalTokens;
                    if (toUpdate.added)
                        uow.OwnerOnly.Add(toUpdate.actualItem);
                    else
                        uow.OwnerOnly.Update(toUpdate.actualItem);
                    await uow.SaveChangesAsync();

                    var embedsCount = Math.Max(1, (int)Math.Ceiling((double)response.Length / 4096));

                    var embeds = new List<EmbedBuilder>(Math.Min(embedsCount, 10));

                    for (var i = 0; i < embeds.Capacity; i++)
                    {
                        var messagePart = response.Substring(i * 4096, Math.Min(4096, response.Length - i * 4096));

                        var embedBuilder = new EmbedBuilder();

                        if (i == 0)
                        {
                            embedBuilder.WithOkColor()
                                .WithDescription(messagePart)
                                .WithAuthor("ChatGPT",
                                    "https://seeklogo.com/images/C/chatgpt-logo-02AFA704B5-seeklogo.com.png");
                        }
                        else
                        {
                            embedBuilder.WithDescription(messagePart);
                        }

                        if (i == embeds.Capacity - 1)
                        {
                            embedBuilder.WithFooter(
                                $"Requested by {args.Author} | Response Tokens: {conversation.MostResentAPIResult.Usage.TotalTokens} | Total Used: {toUpdate.actualItem.GptTokensUsed}");
                        }

                        embeds.Add(embedBuilder);
                    }

                    var embedArray = embeds.Select(e => e.Build()).ToArray();

                    await webhook.ModifyMessageAsync(msg, properties =>
                    {
                        properties.Embeds = embedArray;
                    });
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    await usrMsg.SendErrorReplyAsync(
                        "Something went wrong, please try again later. Probably a bad webhook.");
                }
            }
            else
            {
                var msg = await usrMsg.SendConfirmReplyAsync($"{bss.Data.LoadingEmote} awaiting response...");
                var response = await conversation.GetResponseFromChatbotAsync();
                toUpdate.actualItem.GptTokensUsed += conversation.MostResentAPIResult.Usage.TotalTokens;
                if (toUpdate.added)
                    uow.OwnerOnly.Add(toUpdate.actualItem);
                else
                    uow.OwnerOnly.Update(toUpdate.actualItem);
                await uow.SaveChangesAsync();

                var embedsCount = Math.Max(1, (int)Math.Ceiling((double)response.Length / 4096));

                var embeds = new List<EmbedBuilder>(Math.Min(embedsCount, 10));

                for (var i = 0; i < embeds.Capacity; i++)
                {
                    var messagePart = response.Substring(i * 4096, Math.Min(4096, response.Length - i * 4096));

                    var embedBuilder = new EmbedBuilder();

                    if (i == 0)
                    {
                        embedBuilder.WithOkColor()
                            .WithDescription(messagePart)
                            .WithAuthor("ChatGPT",
                                "https://seeklogo.com/images/C/chatgpt-logo-02AFA704B5-seeklogo.com.png");
                    }
                    else
                    {
                        embedBuilder.WithDescription(messagePart);
                    }

                    if (i == embeds.Capacity - 1)
                    {
                        embedBuilder.WithFooter(
                            $"Requested by {args.Author} | Response Tokens: {conversation.MostResentAPIResult.Usage.TotalTokens} | Total Used: {toUpdate.actualItem.GptTokensUsed}");
                    }

                    embeds.Add(embedBuilder);
                }

                var embedArray = embeds.Select(e => e.Build()).ToArray();

                await msg.ModifyAsync(x => x.Embeds = embedArray);
            }
        }
        else
        {
            Model modelToUse;
            switch (bss.Data.ChatGptModel)
            {
                case "gpt-4-0613":
                    modelToUse = Model.GPT4_32k_Context;
                    break;
                case "gpt4":
                case "gpt-4":
                    modelToUse = Model.GPT4;
                    break;
                case "gpt-3":
                case "gpt3":
                    modelToUse = Model.ChatGPTTurbo;
                    break;
                default:
                    modelToUse = Model.ChatGPTTurbo;
                    break;
            }

            var chat = api.Chat.CreateConversation(new ChatRequest
            {
                MaxTokens = bss.Data.ChatGptMaxTokens, Temperature = bss.Data.ChatGptTemperature, Model = modelToUse
            });
            chat.AppendSystemMessage(bss.Data.ChatGptInitPrompt);
            chat.AppendSystemMessage($"The users name is {args.Author}.");
            chat.AppendUserInput(args.Content);
            try
            {
                conversations.TryAdd(args.Author.Id, chat);
                if (!string.IsNullOrEmpty(bss.Data.ChatGptWebhook))
                {
                    var webhook = new DiscordWebhookClient(bss.Data.ChatGptWebhook);
                    var msg = await webhook.SendConfirmAsync($"{bss.Data.LoadingEmote} awaiting response...");
                    var response = await chat.GetResponseFromChatbotAsync();
                    toUpdate.actualItem.GptTokensUsed += chat.MostResentAPIResult.Usage.TotalTokens;
                    if (toUpdate.added)
                        uow.OwnerOnly.Add(toUpdate.actualItem);
                    else
                        uow.OwnerOnly.Update(toUpdate.actualItem);

                    var embedsCount = Math.Max(1, (int)Math.Ceiling((double)response.Length / 4096));

                    var embeds = new List<EmbedBuilder>(Math.Min(embedsCount, 10));

                    for (var i = 0; i < embeds.Capacity; i++)
                    {
                        var messagePart = response.Substring(i * 4096, Math.Min(4096, response.Length - i * 4096));

                        var embedBuilder = new EmbedBuilder();

                        if (i == 0)
                        {
                            embedBuilder.WithOkColor()
                                .WithDescription(messagePart)
                                .WithAuthor("ChatGPT",
                                    "https://seeklogo.com/images/C/chatgpt-logo-02AFA704B5-seeklogo.com.png");
                        }
                        else
                        {
                            embedBuilder.WithDescription(messagePart);
                        }

                        if (i == embeds.Capacity - 1)
                        {
                            embedBuilder.WithFooter(
                                $"Requested by {args.Author} | Response Tokens: {chat.MostResentAPIResult.Usage.TotalTokens} | Total Used: {toUpdate.actualItem.GptTokensUsed}");
                        }

                        embeds.Add(embedBuilder);
                    }

                    var embedArray = embeds.Select(e => e.Build()).ToArray();

                    await webhook.ModifyMessageAsync(msg, properties =>
                    {
                        properties.Embeds = embedArray;
                    });
                }
                else
                {
                    var msg = await usrMsg.SendConfirmReplyAsync($"{bss.Data.LoadingEmote} awaiting response...");
                    var response = await chat.GetResponseFromChatbotAsync();
                    toUpdate.actualItem.GptTokensUsed += chat.MostResentAPIResult.Usage.TotalTokens;
                    if (toUpdate.added)
                        uow.OwnerOnly.Add(toUpdate.actualItem);
                    else
                        uow.OwnerOnly.Update(toUpdate.actualItem);

                    var embedsCount = Math.Max(1, (int)Math.Ceiling((double)response.Length / 4096));

                    var embeds = new List<EmbedBuilder>(Math.Min(embedsCount, 10));

                    for (var i = 0; i < embeds.Capacity; i++)
                    {
                        var messagePart = response.Substring(i * 4096, Math.Min(4096, response.Length - i * 4096));

                        var embedBuilder = new EmbedBuilder();

                        if (i == 0)
                        {
                            embedBuilder.WithOkColor()
                                .WithDescription(messagePart)
                                .WithAuthor("ChatGPT",
                                    "https://seeklogo.com/images/C/chatgpt-logo-02AFA704B5-seeklogo.com.png");
                        }
                        else
                        {
                            embedBuilder.WithDescription(messagePart);
                        }

                        if (i == embeds.Capacity - 1)
                        {
                            embedBuilder.WithFooter(
                                $"Requested by {args.Author} | Response Tokens: {chat.MostResentAPIResult.Usage.TotalTokens} | Total Used: {toUpdate.actualItem.GptTokensUsed}");
                        }

                        embeds.Add(embedBuilder);
                    }

                    var embedArray = embeds.Select(e => e.Build()).ToArray();

                    await msg.ModifyAsync(m => m.Embeds = embedArray);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await usrMsg.SendErrorReplyAsync("Something went wrong, please try again later.");
            }
        }
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
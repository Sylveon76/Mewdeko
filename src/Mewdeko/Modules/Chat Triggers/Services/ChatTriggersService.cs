﻿using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using LinqToDB.EntityFrameworkCore;
using Mewdeko.Common.DiscordImplementations;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Common.PubSub;
using Mewdeko.Common.Yml;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Chat_Triggers.Common;
using Mewdeko.Modules.Chat_Triggers.Extensions;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services.Settings;
using Mewdeko.Services.strings;
using Microsoft.EntityFrameworkCore;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using CTModel = Mewdeko.Database.Models.ChatTriggers;

namespace Mewdeko.Modules.Chat_Triggers.Services;

public sealed class ChatTriggersService : IEarlyBehavior, INService, IReadyExecutor
{
    public enum CtField
    {
        AutoDelete,
        DmResponse,
        AllowTarget,
        ContainsAnywhere,
        Message,
        ReactToTrigger,
        NoRespond,
        PermsEnabledByDefault,
        ChannelsEnabledByDefault
    }

    private const string MentionPh = "%bot.mention%";

    private const string PrependExport =
        @"# WARNING: crossposting information is not saved.
# Keys are triggers, Each key has a LIST of custom reactions in the following format:
# - res: Response string
#   react:
#     - <List
#     -  of
#     - reactions>
#   at: Whether custom reaction allows targets (see .h .crat)
#   ca: Whether custom reaction expects trigger anywhere (see .h .crca)
#   dm: Whether custom reaction DMs the response (see .h .crdm)
#   ad: Whether custom reaction automatically deletes triggering message (see .h .crad)
#   rtt: Whether custom reaction emotes are added to the response or trigger

";

    private static readonly ISerializer ExportSerializer = new SerializerBuilder()
        .WithEventEmitter(args => new MultilineScalarFlowStyleEmitter(args))
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithIndentedSequences()
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
        .DisableAliases()
        .Build();

    private readonly DiscordSocketClient client;
    private readonly CmdCdService cmdCds;
    private readonly TypedKey<bool> crsReloadedKey = new("crs.reloaded");

    private readonly DbService db;

    private readonly TypedKey<CTModel> gcrAddedKey = new("gcr.added");
    private readonly TypedKey<int> gcrDeletedkey = new("gcr.deleted");
    private readonly TypedKey<CTModel> gcrEditedKey = new("gcr.edited");
    private readonly TypedKey<CTModel> crAdded = new("cr.added");

    private readonly object gcrWriteLock = new();
    private readonly GlobalPermissionService gperm;
    private readonly DiscordPermOverrideService discordPermOverride;
    private readonly PermissionService perms;
    private readonly IPubSub pubSub;
    private readonly Random rng;
    private readonly IBotStrings strings;
    private readonly GuildSettingsService guildSettings;
    private readonly BotConfigService configService;
    private readonly IBotCredentials creds;

    // it is perfectly fine to have global chattriggers as an array
    // 1. custom reactions are almost never added (compared to how many times they are being looped through)
    // 2. only need write locks for this as we'll rebuild+replace the array on every edit
    // 3. there's never many of them (at most a thousand, usually < 100)
    private CTModel[] globalReactions;
    private ConcurrentDictionary<ulong, CTModel[]> newGuildReactions;

    private bool ready;

    public ChatTriggersService(
        PermissionService perms,
        DbService db,
        IBotStrings strings,
        Mewdeko bot,
        DiscordSocketClient client,
        GlobalPermissionService gperm,
        CmdCdService cmdCds,
        IPubSub pubSub,
        DiscordPermOverrideService discordPermOverride,
        GuildSettingsService guildSettings,
        BotConfigService configService, IBotCredentials creds)
    {
        this.db = db;
        this.client = client;
        this.perms = perms;
        this.strings = strings;
        this.cmdCds = cmdCds;
        this.gperm = gperm;
        this.pubSub = pubSub;
        this.discordPermOverride = discordPermOverride;
        this.guildSettings = guildSettings;
        this.configService = configService;
        this.creds = creds;
        rng = new MewdekoRandom();

        pubSub.Sub(crsReloadedKey, OnCrsShouldReload);
        pubSub.Sub(gcrAddedKey, OnGcrAdded);
        pubSub.Sub(gcrDeletedkey, OnGcrDeleted);
        pubSub.Sub(gcrEditedKey, OnGcrEdited);
        pubSub.Sub(crAdded, OnCrAdded);

        bot.JoinedGuild += OnJoinedGuild;
        this.client.LeftGuild += OnLeftGuild;
    }

    private async ValueTask OnCrAdded(CTModel arg)
    {
        await AddAsync(arg.GuildId, arg.Trigger, arg.Response, arg.IsRegex == 1);
    }

    public int Priority => -1;
    public ModuleBehaviorType BehaviorType => ModuleBehaviorType.Executor;

    public async Task<bool> RunBehavior(DiscordSocketClient socketClient, IGuild guild, IUserMessage msg)
    {
        // maybe this message is a custom reaction
        var ct = await TryGetChatTriggers(msg);

        if (ct is null)
            return false;
        if (await cmdCds.TryBlock(guild, msg.Author, ct.Trigger).ConfigureAwait(false))
            return false;
        if (!ct.ValidTriggerTypes.HasFlag(ChatTriggerType.Message))
            return false;

        try
        {
            if (gperm.BlockedModules.Contains("ActualChatTriggers"))
                return true;

            if (guild is SocketGuild sg)
            {
                var pc = await this.perms.GetCacheFor(guild.Id);
                if (!pc.Permissions.CheckPermissions(msg, ct.Trigger, "ActualChatTriggers",
                        out var index))
                {
                    if (pc.Verbose)
                    {
                        var returnMsg = strings.GetText("perm_prevent", sg.Id,
                            index + 1,
                            Format.Bold(pc.Permissions[index].GetCommand(await guildSettings.GetPrefix(guild), sg)));
                        try
                        {
                            await msg.Channel.SendErrorAsync(returnMsg).ConfigureAwait(false);
                        }
                        catch
                        {
                            // ignored
                        }

                        Log.Information(returnMsg);
                    }

                    return true;
                }

                if (discordPermOverride.TryGetOverrides(guild.Id, ct.Trigger, out var guildPermission))
                {
                    var user = msg.Author as IGuildUser;
                    if (!user.GuildPermissions.Has(guildPermission))
                    {
                        Log.Information("Chat Trigger {CtTrigger} Blocked for {MsgAuthor} in {Guild} due to them missing {Perms}.", ct.Trigger, msg.Author, guild, guildPermission);
                        return false;
                    }
                }
            }

            var guildConfig = await guildSettings.GetGuildConfig(guild.Id);
            var uow = db.GetDbContext();
            var dbUser = await uow.GetOrCreateUser(msg.Author);
            if (!false.ParseBoth(guildConfig.StatsOptOut.ToString()) && dbUser.StatsOptOut == 0)
            {
                var toAdd = new CommandStats
                {
                    ChannelId = msg.Channel.Id,
                    Trigger = 1,
                    NameOrId = $"{ct.Id}",
                    GuildId = guild.Id,
                    UserId = msg.Author.Id
                };
                await uow.CommandStats.AddAsync(toAdd);
                await uow.SaveChangesAsync();
            }

            var sentMsg = await ct.Send(msg, this.client, false, uow).ConfigureAwait(false);

            foreach (var reaction in ct.GetReactions())
            {
                try
                {
                    if (ct.ReactToTrigger == 0 && ct.NoRespond == 0)
                        await sentMsg.AddReactionAsync(reaction.ToIEmote()).ConfigureAwait(false);
                    else
                        await msg.AddReactionAsync(reaction.ToIEmote()).ConfigureAwait(false);
                }
                catch
                {
                    Log.Warning("Unable to add reactions to message {Message} in server {GuildId}", sentMsg.Id,
                        ct.GuildId);
                    break;
                }

                await Task.Delay(1000).ConfigureAwait(false);
            }

            try
            {
                if (ct.AutoDeleteTrigger == 1)
                    await msg.DeleteAsync().ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }

            if (ct.GuildId is null || msg?.Author is not IGuildUser guildUser)
                return true;
            {
                var effectedUsers = ct.RoleGrantType switch
                {
                    CtRoleGrantType.Mentioned => msg.Content.GetUserMentions().Take(5),
                    CtRoleGrantType.Sender => new List<ulong>
                    {
                        msg.Author.Id
                    },
                    CtRoleGrantType.Both => msg.Content.GetUserMentions().Take(4).Append(msg.Author.Id),
                    _ => new List<ulong>()
                };

                foreach (var userId in effectedUsers)
                {
                    var user = await guildUser.Guild.GetUserAsync(userId).ConfigureAwait(false);
                    try
                    {
                        var baseRoles = user.RoleIds.Where(x => x != guild.EveryoneRole.Id).ToList();
                        var roles = baseRoles.Where(x => !ct.RemovedRoles?.Contains(x.ToString()) ?? true).ToList();
                        roles.AddRange(ct.GetGrantedRoles().Where(x => !user.RoleIds.Contains(x)));
                        // difference is caused by @everyone
                        if (baseRoles.Any(x => !roles.Contains(x)) || roles.Any(x => !baseRoles.Contains(x)))
                            await user.ModifyAsync(x => x.RoleIds = new(roles)).ConfigureAwait(false);
                    }
                    catch
                    {
                        Log.Warning("Unable to modify the roles of {User} in {GuildId}", guildUser.Id, ct.GuildId);
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex.Message);
        }

        return false;
    }

    public async Task RunInteractionTrigger(SocketInteraction inter, CTModel ct, bool followup = false)
    {
        switch (inter)
        {
            case SocketCommandBase when !ct.ValidTriggerTypes.HasFlag(ChatTriggerType.Interaction):
            case SocketMessageComponent when !ct.ValidTriggerTypes.HasFlag(ChatTriggerType.Button):
                return;
            default:
                try
                {
                    var fakeMsg = new MewdekoUserMessage
                    {
                        Author = inter.User, Content = ct.Trigger, Channel = inter.Channel
                    };

                    if (gperm.BlockedModules.Contains("ActualChatTriggers"))
                        return;

                    if (inter.Channel is IGuildChannel { Guild: SocketGuild guild })
                    {
                        var pc = await this.perms.GetCacheFor(guild.Id);
                        if (!pc.Permissions.CheckPermissions(fakeMsg, ct.Trigger, "ActualChatTriggers",
                                out var index))
                        {
                            if (!pc.Verbose)
                                return;
                            var returnMsg = strings.GetText("perm_prevent", guild.Id,
                                index + 1,
                                Format.Bold(pc.Permissions[index].GetCommand(await guildSettings.GetPrefix(guild), guild)));
                            try
                            {
                                await fakeMsg.Channel.SendErrorAsync(returnMsg).ConfigureAwait(false);
                            }
                            catch
                            {
                                // ignored
                            }

                            Log.Information(returnMsg);

                            return;
                        }

                        if (discordPermOverride.TryGetOverrides(guild.Id, ct.Trigger, out var guildPermission))
                        {
                            var user = inter.User as IGuildUser;
                            if (!user.GuildPermissions.Has(guildPermission))
                            {
                                Log.Information($"Chat Trigger {ct.Trigger} Blocked for {inter.User} in {guild} due to them missing {guildPermission}.");
                                return;
                            }
                        }
                    }

                    var channel = inter.Channel as IGuildChannel;

                    var guildConfig = await guildSettings.GetGuildConfig(channel.GuildId);
                    await using var uow = db.GetDbContext();
                    var dbUser = await uow.GetOrCreateUser(fakeMsg.Author);
                    if (!false.ParseBoth(guildConfig.StatsOptOut) && dbUser.StatsOptOut == 0)
                    {
                        var toAdd = new CommandStats
                        {
                            ChannelId = channel.Id,
                            Trigger = 1,
                            NameOrId = $"{ct.Id}",
                            GuildId = channel.GuildId,
                            UserId = inter.User.Id
                        };
                        await uow.CommandStats.AddAsync(toAdd);
                        await uow.SaveChangesAsync();
                    }

                    var sentMsg = await ct.SendInteraction(inter, this.client, false, fakeMsg, false.ParseBoth(ct.EphemeralResponse), uow, followup).ConfigureAwait(false);

                    foreach (var reaction in ct.GetReactions())
                    {
                        try
                        {
                            if (ct.ReactToTrigger == 0 && ct.NoRespond == 0)
                                await sentMsg.AddReactionAsync(reaction.ToIEmote()).ConfigureAwait(false);
                            else
                                await sentMsg.AddReactionAsync(reaction.ToIEmote()).ConfigureAwait(false);
                        }
                        catch
                        {
                            Log.Warning("Unable to add reactions to message {Message} in server {GuildId}", sentMsg.Id,
                                ct.GuildId);
                            break;
                        }

                        await Task.Delay(1000).ConfigureAwait(false);
                    }

                    if (ct.GuildId is null || inter.User is not IGuildUser guildUser)
                        return;
                    {
                        var effectedUsers = inter is SocketUserCommand uCmd
                            ? ct.RoleGrantType switch
                            {
                                CtRoleGrantType.Mentioned => new List<ulong>
                                {
                                    uCmd.Data.Member.Id
                                },
                                CtRoleGrantType.Sender => new List<ulong>
                                {
                                    uCmd.User.Id
                                },
                                CtRoleGrantType.Both => new List<ulong>
                                {
                                    uCmd.User.Id, uCmd.Data.Member.Id
                                },
                                _ => new List<ulong>()
                            }
                            : ct.RoleGrantType switch
                            {
                                CtRoleGrantType.Mentioned => new(),
                                CtRoleGrantType.Sender => new List<ulong>
                                {
                                    inter.User.Id
                                },
                                CtRoleGrantType.Both => new List<ulong>
                                {
                                    inter.User.Id
                                },
                                _ => new List<ulong>()
                            };

                        foreach (var userId in effectedUsers)
                        {
                            var user = await guildUser.Guild.GetUserAsync(userId).ConfigureAwait(false);
                            try
                            {
                                var baseRoles = user.RoleIds.Where(x => x != guildUser.Guild?.EveryoneRole.Id).ToList();
                                var roles = baseRoles.Where(x => !ct.RemovedRoles?.Contains(x.ToString()) ?? true).ToList();
                                roles.AddRange(ct.GetGrantedRoles().Where(x => !user.RoleIds.Contains(x)));

                                // difference is caused by @everyone
                                if (baseRoles.Any(x => !roles.Contains(x)) || roles.Any(x => !baseRoles.Contains(x)))
                                    await user.ModifyAsync(x => x.RoleIds = new(roles)).ConfigureAwait(false);
                            }
                            catch
                            {
                                Log.Warning("Unable to modify the roles of {User} in {GuildId}", guildUser.Id, ct.GuildId);
                            }
                        }
                    }

                    return;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex.Message);
                }

                return;
        }
    }

    public string ExportCrs(ulong? guildId)
    {
        var crs = GetChatTriggersFor(guildId);

        var crsDict = crs
            .GroupBy(x => x.Trigger)
            .ToDictionary(x => x.Key, x => x.Select(ExportedTriggers.FromModel));

        return PrependExport + ExportSerializer
            .Serialize(crsDict)
            .UnescapeUnicodeCodePoints();
    }

    public async Task<bool> ImportCrsAsync(IGuildUser user, string input)
    {
        try
        {
            Dictionary<string, List<ExportedTriggers>> data;
            try
            {
                data = Yaml.Deserializer.Deserialize<Dictionary<string, List<ExportedTriggers>>>(input);
                if (data.Sum(x => x.Value.Count) == 0)
                    return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                return false;
            }

            await using var uow = db.GetDbContext();
            List<CTModel> triggers = new();
            foreach (var (trigger, value) in data)
            {
                triggers.AddRange(value
                    .Where(ct => !string.IsNullOrWhiteSpace(ct.Res))
                    .Select(ct => new CTModel
                    {
                        GuildId = user.Guild.Id,
                        Response = ct.Res,
                        Reactions = ct.React?.JoinWith("@@@"),
                        Trigger = trigger,
                        AllowTarget = ct.At ? 1 : 0,
                        ContainsAnywhere = ct.Ca ? 1 : 0,
                        DmResponse = ct.Dm ? 1 : 0,
                        AutoDeleteTrigger = ct.Ad ? 1 : 0,
                        NoRespond = ct.Nr ? 1 : 0,
                        IsRegex = ct.Rgx ? 1 : 0,
                        GrantedRoles = string.Join("@@@", ct.ARole.Select(x => x.ToString())),
                        RemovedRoles = string.Join("@@@", ct.RRole.Select(x => x.ToString())),
                        ReactToTrigger = ct.Rtt ? 1 : 0,
                        RoleGrantType = ct.Rgt,
                        ValidTriggerTypes = ct.VTypes,
                        ApplicationCommandName = ct.AcName,
                        ApplicationCommandDescription = ct.AcDesc,
                        ApplicationCommandType = ct.Act,
                        EphemeralResponse = ct.Eph ? 1 : 0
                    }));
            }

            List<ulong> roles = new();
            triggers.ForEach(x => roles.AddRange(x.GetGrantedRoles()));
            triggers.ForEach(x => roles.AddRange(x.GetRemovedRoles()));

            if (roles.Count > 0 && roles.All(y => user.Guild.GetRole(y).CanManageRole(user)))
                return false;

            await uow.ChatTriggers.AddRangeAsync(triggers).ConfigureAwait(false);
            await uow.SaveChangesAsync().ConfigureAwait(false);
            await TriggerReloadChatTriggers().ConfigureAwait(false);
        }

        catch (Exception ex)
        {
            Log.Error(ex.ToString());
            return false;
        }

        return true;
    }

    private async Task ReloadInternal(IReadOnlyList<ulong> allGuildIds)
    {
        await using var uow = db.GetDbContext();
        var guildItems = await uow.ChatTriggers
            .ToLinqToDB().Where(x => (int)(x.GuildId / (ulong)Math.Pow(2, 22) % (ulong)creds.TotalShards) == client.ShardId)
            .AsNoTracking().ToListAsyncLinqToDB();

        newGuildReactions = guildItems
            .GroupBy(k => k.GuildId!.Value)
            .ToDictionary(g => g.Key,
                g => g.Select(x =>
                {
                    x.Trigger = x.Trigger.Replace(MentionPh, client.CurrentUser.Mention);
                    return x;
                }).ToArray())
            .ToConcurrent();

        lock (gcrWriteLock)
        {
            globalReactions = uow.ChatTriggers
                .AsNoTracking()
                .Where(x => x.GuildId == null || x.GuildId == 0)
                .AsEnumerable()
                .Select(x =>
                {
                    x.Trigger = x.Trigger.Replace(MentionPh, client.CurrentUser.Mention);
                    return x;
                })
                .ToArray();
        }

        ready = true;
    }

    private async Task<CTModel?> TryGetChatTriggers(IUserMessage umsg)
    {
        if (!ready)
            return null;

        if (umsg.Channel is not SocketTextChannel channel)
            return null;

        var content = umsg.Content.Trim().ToLowerInvariant();

        if (newGuildReactions.TryGetValue(channel.Guild.Id, out var reactions) && reactions.Length > 0)
        {
            var cr = await MatchChatTriggers(content, reactions, channel.Guild);
            if (cr is not null)
                return cr;
        }

        // ReSharper disable once InconsistentlySynchronizedField
        var localGrs = globalReactions;

        return await MatchChatTriggers(content, localGrs, channel.Guild);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task<CTModel?> MatchChatTriggers(string content, CTModel[] crs, SocketGuild guild)
    {
        var guildPrefix = await guildSettings.GetPrefix(guild);
        var globalPrefix = configService.Data.Prefix;

        var result = new List<CTModel>(1);
        foreach (var ct in crs)
        {
            var trigger = ct.Trigger;

            switch (ct.PrefixType)
            {
                case RequirePrefixType.Custom:
                    if (!content.StartsWith(ct.CustomPrefix))
                        continue;
                    content = content[ct.CustomPrefix.Length..];
                    break;
                case RequirePrefixType.GuildOrNone:
                    if (guildPrefix is null || !content.StartsWith(guildPrefix))
                        continue;
                    content = content[guildPrefix.Length..];
                    break;
                case RequirePrefixType.GuildOrGlobal:
                    if (!content.StartsWith(guildPrefix ?? globalPrefix))
                        continue;
                    content = content[(guildPrefix ?? globalPrefix).Length..];
                    break;
                case RequirePrefixType.Global:
                    if (!content.StartsWith(globalPrefix))
                        continue;
                    content = content[globalPrefix.Length..];
                    break;
                case RequirePrefixType.None:
                default:
                    break;
            }

            // regex triggers are only checked on regex content
            if (ct.IsRegex == 1)
            {
                if (Regex.IsMatch(new string(content), trigger, RegexOptions.None, TimeSpan.FromMilliseconds(1)))
                    result.Add(ct);
                continue;
            }

            // if the trigger depends on user mentions to grant roles
            // those should be removed
            if (ct.RoleGrantType is CtRoleGrantType.Mentioned or CtRoleGrantType.Both)
            {
                content = content.RemoveUserMentions().Trim();
            }

            if (content.Length > trigger.Length)
            {
                // if input is greater than the trigger, it can only work if:
                // it has CA enabled
                if (ct.ContainsAnywhere == 1)
                {
                    // if ca is enabled, we have to check if it is a word within the content
                    var wp = Extensions.Extensions.GetWordPosition(content, trigger);

                    // if it is, then that's valid
                    if (wp != WordPosition.None)
                        result.Add(ct);

                    // if it's not, then it cant' work under any circumstance,
                    // because content is greater than the trigger length
                    // so it can't be equal, and it's not contained as a word
                    continue;
                }

                // if CA is disabled, and CR has AllowTarget, then the
                // content has to start with the trigger followed by a space
                if (ct.AllowTarget == 1 && content.StartsWith(trigger, StringComparison.OrdinalIgnoreCase)
                                        && content[trigger.Length] == ' ')
                {
                    result.Add(ct);
                }
            }
            else if (content.Length < ct.Trigger.Length)
            {
                // if input length is less than trigger length, it means
                // that the reaction can never be triggered
            }
            else
            {
                // if input length is the same as trigger length
                // reaction can only trigger if the strings are equal
                if (content.SequenceEqual(ct.Trigger))
                    result.Add(ct);
            }
        }

        return result.Count == 0 ? null : result[rng.Next(0, result.Count)];
    }

    public async Task ResetCrReactions(ulong? maybeGuildId, int id)
    {
        await using var uow = db.GetDbContext();
        var ct = await uow.ChatTriggers.GetById(id);
        if (ct is null)
            return;

        ct.Reactions = string.Empty;

        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    private async Task UpdateInternalAsync(ulong? maybeGuildId, CTModel ct)
    {
        if (maybeGuildId is { } guildId)
            UpdateInternal(guildId, ct);
        else
        {
            _ = pubSub.Pub(gcrEditedKey, ct);
            return;
        }

        // handle interaction updates
        if (ct.ApplicationCommandType == CtApplicationCommandType.None)
            return;

        var guild = client.GetGuild(guildId);
        await RegisterTriggersToGuildAsync(guild).ConfigureAwait(false);
    }

    private void UpdateInternal(ulong? maybeGuildId, CTModel ct)
    {
        if (maybeGuildId is { } guildId)
        {
            newGuildReactions.AddOrUpdate(guildId, new[]
                {
                    ct
                },
                (_, old) =>
                {
                    var newArray = old.ToArray();
                    for (var i = 0; i < newArray.Length; i++)
                    {
                        if (newArray[i].Id == ct.Id)
                            newArray[i] = ct;
                    }

                    return newArray;
                });
        }
        else
        {
            lock (gcrWriteLock)
            {
                var crs = globalReactions;
                for (var i = 0; i < crs.Length; i++)
                {
                    if (crs[i].Id == ct.Id)
                        crs[i] = ct;
                }
            }
        }
    }

    private Task AddInternalAsync(ulong? maybeGuildId, CTModel ct)
    {
        // only do this for perf purposes
        ct.Trigger = ct.Trigger.Replace(MentionPh, client.CurrentUser.Mention);

        if (maybeGuildId is { } guildId)
        {
            newGuildReactions.AddOrUpdate(guildId,
                new[]
                {
                    ct
                },
                (_, old) => old.With(ct));
        }
        else
        {
            return pubSub.Pub(gcrAddedKey, ct);
        }

        return Task.CompletedTask;
    }

    private Task DeleteInternalAsync(ulong? maybeGuildId, int id)
    {
        if (maybeGuildId is { } guildId)
        {
            newGuildReactions.AddOrUpdate(guildId,
                Array.Empty<CTModel>(),
                (_, old) => DeleteInternal(old, id));

            return Task.CompletedTask;
        }

        lock (gcrWriteLock)
        {
            var cr = Array.Find(globalReactions, item => item.Id == id);
            if (cr is not null)
                return pubSub.Pub(gcrDeletedkey, cr.Id);
        }

        return Task.CompletedTask;
    }

    private static CTModel[] DeleteInternal(IReadOnlyList<CTModel>? cts, int id)
    {
        if (cts is null || cts.Count == 0)
            return cts as CTModel[] ?? cts?.ToArray();

        var newCrs = new CTModel[cts.Count - 1];
        for (int i = 0, k = 0; i < cts.Count; i++, k++)
        {
            if (cts[i].Id == id)
            {
                k--;
                continue;
            }

            newCrs[k] = cts[i];
        }

        return newCrs;
    }

    public async Task SetCrReactions(ulong? guildId, int id, IEnumerable<string> emojis)
    {
        CTModel ct;
        var uow = db.GetDbContext();
        await using (uow.ConfigureAwait(false))
        {
            ct = await uow.ChatTriggers.GetById(id);
            if (ct is null)
                return;

            ct.Reactions = string.Join("@@@", emojis);

            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        await UpdateInternalAsync(guildId, ct).ConfigureAwait(false);
    }

    public async Task<(bool Success, bool NewValue)> ToggleCrOptionAsync(CTModel? ct, CtField? field)
    {
        long newVal = 0;
        await using var uow = db.GetDbContext();

        if (ct is null)
            return (false, false);

        newVal = field switch
        {
            CtField.AutoDelete => ct.AutoDeleteTrigger ^= 1,
            CtField.ContainsAnywhere => ct.ContainsAnywhere ^= 1,
            CtField.DmResponse => ct.DmResponse ^= 1,
            CtField.AllowTarget => ct.AllowTarget ^= 1,
            CtField.ReactToTrigger => ct.ReactToTrigger ^= 1,
            CtField.NoRespond => ct.NoRespond ^= 1,
            _ => newVal
        };

        uow.ChatTriggers.Update(ct);
        await uow.SaveChangesAsync().ConfigureAwait(false);

        await UpdateInternalAsync(ct.GuildId, ct).ConfigureAwait(false);

        return (true, false.ParseBoth(newVal));
    }


    public async Task<CTModel?> GetChatTriggers(ulong? guildId, int id)
    {
        await using var uow = db.GetDbContext();
        var ct = await uow.ChatTriggers.GetById(id);
        if (ct == null || ct.GuildId != guildId)
            return null;

        return ct;
    }

    public async Task<CTModel?> GetGuildOrGlobalTriggers(ulong? guildId, int id)
    {
        await using var uow = db.GetDbContext();
        var ct = await uow.ChatTriggers.GetById(id);
        if (ct == null || (ct.GuildId != guildId && ct.GuildId is not 0 or null))
            return null;

        return ct;
    }

    public int DeleteAllChatTriggers(ulong guildId)
    {
        using var uow = db.GetDbContext();
        var count = uow.ChatTriggers.ClearFromGuild(guildId);
        uow.SaveChanges();

        newGuildReactions.TryRemove(guildId, out _);

        return count;
    }

    public async Task<bool> ReactionExists(ulong? guildId, string input)
    {
        await using var uow = db.GetDbContext();
        var ct = await uow.ChatTriggers.GetByGuildIdAndInput(guildId, input);
        return ct != null;
    }

    public Task OnReadyAsync() => ReloadInternal(client.Guilds.Select(x => x.Id).ToList());

    private ValueTask OnCrsShouldReload(bool _) => new(ReloadInternal(client.Guilds.Select(x => x.Id).ToList()));

    private ValueTask OnGcrAdded(CTModel c)
    {
        lock (gcrWriteLock)
        {
            var newGlobalReactions = new CTModel[globalReactions.Length + 1];
            Array.Copy(globalReactions, newGlobalReactions, globalReactions.Length);
            newGlobalReactions[globalReactions.Length] = c;
            globalReactions = newGlobalReactions;
        }

        return default;
    }

    private ValueTask OnGcrEdited(CTModel c)
    {
        lock (gcrWriteLock)
        {
            for (var i = 0; i < globalReactions.Length; i++)
            {
                if (globalReactions[i].Id != c.Id)
                    continue;
                globalReactions[i] = c;
                return default;
            }

            // if edited ct is not found?!
            // add it
            OnGcrAdded(c);
        }

        return default;
    }

    private ValueTask OnGcrDeleted(int id)
    {
        lock (gcrWriteLock)
        {
            globalReactions = DeleteInternal(globalReactions, id);
        }

        return default;
    }

    public Task TriggerReloadChatTriggers() => pubSub.Pub(crsReloadedKey, true);

    private Task OnLeftGuild(SocketGuild arg)
    {
        newGuildReactions.TryRemove(arg.Id, out _);

        return Task.CompletedTask;
    }

    private async Task OnJoinedGuild(GuildConfig gc)
    {
        await using var uow = db.GetDbContext();
        newGuildReactions[gc.GuildId] = await
            uow
                .ChatTriggers
                .AsNoTracking()
                .Where(x => x.GuildId == gc.GuildId)
                .ToArrayAsync().ConfigureAwait(false);
    }

    public async Task<CTModel?> AddAsync(ulong? guildId, string key, string? message, bool regex)
    {
        key = key.ToLowerInvariant();
        var cr = new CTModel
        {
            GuildId = guildId, Trigger = key, Response = message, IsRegex = regex ? 1 : 0
        };

        if (cr.Response.Contains("%target", StringComparison.OrdinalIgnoreCase))
            cr.AllowTarget = 1;

        var uow = db.GetDbContext();
        await using (uow.ConfigureAwait(false))
        {
            uow.ChatTriggers.Add(cr);
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        await AddInternalAsync(guildId, cr).ConfigureAwait(false);

        return cr;
    }

    public async Task<CTModel?> EditAsync(ulong? guildId, int id, string? message, bool? regex, string? trigger = null)
    {
        using var uow = db.GetDbContext();
        var ct = await uow.ChatTriggers.GetById(id);

        if (ct == null || ct.GuildId != guildId)
            return null;

        ct.IsRegex = regex.HasValue ? Convert.ToInt64(regex.Value) : ct.IsRegex;

        // disable allowtarget if message had target, but it was removed from it
        if (!message.Contains("%target%", StringComparison.OrdinalIgnoreCase)
            && ct.Response.Contains("%target%", StringComparison.OrdinalIgnoreCase))
        {
            ct.AllowTarget = 0; // false
        }

        ct.Response = message;
        ct.Trigger = trigger ?? ct.Trigger;

        // enable allow target if message is edited to contain target
        if (ct.Response.Contains("%target%", StringComparison.OrdinalIgnoreCase))
            ct.AllowTarget = 1; // true

        await uow.SaveChangesAsync().ConfigureAwait(false);
        await UpdateInternalAsync(guildId.Value, ct).ConfigureAwait(false);

        return ct;
    }


    public async Task<CTModel?> DeleteAsync(ulong? guildId, int id)
    {
        await using var uow = db.GetDbContext();
        var toDelete = await uow.ChatTriggers.GetById(id);

        if (toDelete is null)
            return null;

        if ((toDelete.IsGlobal() && guildId == null) || guildId == toDelete.GuildId)
        {
            uow.ChatTriggers.Remove(toDelete);
            await uow.SaveChangesAsync().ConfigureAwait(false);
            await DeleteInternalAsync(guildId, id).ConfigureAwait(false);
            return toDelete;
        }

        return null;
    }

    public async Task<CTModel?> SetRoleGrantType(ulong? guildId, int id, CtRoleGrantType type)
    {
        await using var uow = db.GetDbContext();
        var ct = await uow.ChatTriggers.GetById(id);

        if (ct == null || ct.GuildId != guildId)
            return null;

        ct.RoleGrantType = type;

        await uow.SaveChangesAsync().ConfigureAwait(false);
        await UpdateInternalAsync(guildId, ct).ConfigureAwait(false);

        return ct;
    }

    public async Task<CTModel?> SetInteractionType(ulong? guildId, int id, CtApplicationCommandType type)
    {
        await using var uow = db.GetDbContext();
        var ct = await uow.ChatTriggers.GetById(id);

        if (ct == null || ct.GuildId != guildId)
            return null;

        ct.ApplicationCommandType = type;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await UpdateInternalAsync(guildId, ct).ConfigureAwait(false);

        return ct;
    }

    public async Task<CTModel?> SetInteractionName(ulong? guildId, int id, string name)
    {
        await using var uow = db.GetDbContext();
        var ct = await uow.ChatTriggers.GetById(id);

        if (ct == null || ct.GuildId != guildId)
            return null;

        ct.ApplicationCommandName = name;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await UpdateInternalAsync(guildId, ct).ConfigureAwait(false);

        return ct;
    }

    public async Task<CTModel?> SetInteractionDescription(ulong? guildId, int id, string description)
    {
        await using var uow = db.GetDbContext();
        var ct = await uow.ChatTriggers.GetById(id);

        if (ct == null || ct.GuildId != guildId)
            return null;

        ct.ApplicationCommandDescription = description;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await UpdateInternalAsync(guildId, ct).ConfigureAwait(false);

        return ct;
    }

    public async Task<CTModel?> SetInteractionEphemeral(ulong? guildId, int id, bool ephemeral)
    {
        await using var uow = db.GetDbContext();
        var ct = await uow.ChatTriggers.GetById(id);

        if (ct == null || ct.GuildId != guildId)
            return null;

        ct.EphemeralResponse = ephemeral ? 1 : 0;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await UpdateInternalAsync(guildId, ct).ConfigureAwait(false);

        return ct;
    }

    public async Task<CTModel?> SetPrefixType(ulong? guildId, int id, RequirePrefixType type)
    {
        await using var uow = db.GetDbContext();
        var ct = await uow.ChatTriggers.GetById(id);

        if (ct == null || ct.GuildId != guildId)
            return null;

        ct.PrefixType = type;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await UpdateInternalAsync(guildId, ct).ConfigureAwait(false);

        return ct;
    }

    public async Task<CTModel?> SetPrefix(ulong? guildId, int id, string name)
    {
        await using var uow = db.GetDbContext();
        var ct = await uow.ChatTriggers.GetById(id);

        if (ct == null || ct.GuildId != guildId)
            return null;

        ct.CustomPrefix = name;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await UpdateInternalAsync(guildId, ct).ConfigureAwait(false);

        return ct;
    }

    public async Task<(CTModel? Trigger, bool Valid)> SetCrosspostingWebhookUrl(ulong? guildId, int id, string webhookUrl, bool bypassTest = false)
    {
        if (!bypassTest)
        {
            try
            {
                using var discordWebhookClient = new DiscordWebhookClient(webhookUrl);
                await discordWebhookClient.SendMessageAsync("Test of chat trigger crossposting webhook!").ConfigureAwait(false);
            }
            catch
            {
                return (null, false);
            }
        }

        await using var uow = db.GetDbContext();
        var ct = await uow.ChatTriggers.GetById(id);

        if (ct == null || ct.GuildId != guildId)
            return (null, true);

        ct.CrosspostingWebhookUrl = webhookUrl;
        ct.CrosspostingChannelId = 0ul;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await UpdateInternalAsync(guildId, ct).ConfigureAwait(false);

        return (ct, true);
    }

    public async Task<CTModel?> SetCrosspostingChannelId(ulong? guildId, int id, ulong channelId)
    {
        await using var uow = db.GetDbContext();
        var ct = await uow.ChatTriggers.GetById(id);

        if (ct == null || ct.GuildId != guildId)
            return null;

        ct.CrosspostingWebhookUrl = "";
        ct.CrosspostingChannelId = channelId;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await UpdateInternalAsync(guildId, ct).ConfigureAwait(false);

        return ct;
    }

    public async Task<CTModel?> SetValidTriggerType(ulong? guildId, int id, ChatTriggerType type, bool enabled)
    {
        await using var uow = db.GetDbContext();
        var ct = await uow.ChatTriggers.GetById(id);

        if (ct == null || ct.GuildId != guildId)
            return null;

        switch (enabled)
        {
            case true when !ct.ValidTriggerTypes.HasFlag(type):
                ct.ValidTriggerTypes |= type;
                break;
            case false when ct.ValidTriggerTypes.HasFlag(type):
                ct.ValidTriggerTypes ^= type;
                break;
        }

        await uow.SaveChangesAsync().ConfigureAwait(false);
        await UpdateInternalAsync(guildId, ct).ConfigureAwait(false);

        return ct;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CTModel[] GetChatTriggersFor(ulong? maybeGuildId)
    {
        if (maybeGuildId is { } guildId and not 0)
        {
            return newGuildReactions.TryGetValue(guildId, out var cts)
                ? cts
                : Array.Empty<CTModel>();
        }

        lock (gcrWriteLock)
        {
            return globalReactions;
        }
    }

    public async Task ToggleGrantedRole(CTModel ct, ulong rId)
    {
        await using var uow = db.GetDbContext();
        var roles = ct.GetGrantedRoles();
        if (!roles.Contains(rId))
            roles.Add(rId);
        else
            roles.RemoveAll(x => x == rId);

        ct.GrantedRoles = string.Join("@@@", roles.Select(x => x.ToString()));
        uow.ChatTriggers.Update(ct);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await UpdateInternalAsync(ct.GuildId, ct).ConfigureAwait(false);
    }

    public async Task ToggleRemovedRole(CTModel ct, ulong rId)
    {
        await using var uow = db.GetDbContext();
        var roles = ct.GetRemovedRoles();
        if (!roles.Contains(rId))
            roles.Add(rId);
        else
            roles.RemoveAll(x => x == rId);

        ct.RemovedRoles = string.Join("@@@", roles.Select(x => x.ToString()));
        uow.ChatTriggers.Update(ct);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await UpdateInternalAsync(ct.GuildId, ct).ConfigureAwait(false);
    }

    public EmbedBuilder GetEmbed(CTModel ct, ulong? gId = null, string? title = null)
    {
        var eb = new EmbedBuilder().WithOkColor()
            .WithTitle(title)
            .WithDescription($"#{ct.Id}")
            .AddField(strings.GetText("ct_interaction_type_title", gId),
                strings.GetText("ct_interaction_type_body", gId, ct.ApplicationCommandType.ToString()))
            .AddField(strings.GetText("ct_realname", gId), ct.RealName)
            .AddField(efb => efb.WithName(strings.GetText("trigger", gId)).WithValue(ct.Trigger.TrimTo(1024)))
            .AddField(efb =>
                efb.WithName(strings.GetText("response", gId))
                    .WithValue($"{(ct.Response + "\n```css\n" + ct.Response).TrimTo(1024 - 11)}```"))
            .AddField(strings.GetText("ct_prefix_type"), ct.PrefixType);
        var reactions = ct.GetReactions();
        if (reactions.Length >= 1)
            eb.AddField(strings.GetText("trigger_reactions", gId), string.Concat(reactions));
        var addedRoles = ct.GetGrantedRoles();
        if (addedRoles.Count >= 1)
            eb.AddField(strings.GetText("added_roles", gId), addedRoles.Select(x => $"<@&{x}>").Aggregate((x, y) => $"{x}, {y}"));
        var removedRoles = ct.GetRemovedRoles();
        if (removedRoles.Count >= 1)
            eb.AddField(strings.GetText("removed_roles", gId), removedRoles.Select(x => $"<@&{x}>").Aggregate((x, y) => $"{x}, {y}"));
        if (addedRoles.Count >= 1 || removedRoles.Count >= 1)
            eb.AddField(strings.GetText("role_grant_type", gId), ct.RoleGrantType);
        if (!ct.ApplicationCommandDescription.IsNullOrWhiteSpace())
            eb.AddField(strings.GetText("ct_interaction_description", gId), ct.ApplicationCommandDescription);
        if (ct.ApplicationCommandId != 0)
            eb.AddField(strings.GetText("ct_interaction_id", gId), ct.ApplicationCommandId.ToString());
        if (ct.ValidTriggerTypes != (ChatTriggerType)0b1111)
            eb.AddField(strings.GetText("ct_valid_fields", gId), ct.ValidTriggerTypes.ToString());
        if (!ct.CrosspostingWebhookUrl.IsNullOrWhiteSpace())
            eb.AddField(strings.GetText("ct_crossposting", gId), strings.GetText("ct_crossposting_webhook"));
        if (ct.CrosspostingChannelId != 0)
            eb.AddField(strings.GetText("ct_crossposting", gId),
                strings.GetText("ct_crossposting_channel", gId, ct.CrosspostingChannelId));
        if (ct.PrefixType == RequirePrefixType.Custom)
            eb.AddField(strings.GetText("ct_custom_prefix", gId), ct.CustomPrefix);
        return eb;
    }

    private record TriggerChildGrouping(string Name, CTModel? Triggers, List<TriggerChildGrouping>? Children);

    public List<ApplicationCommandProperties> GetApplicationCommandProperties(ulong guildId)
    {
        var props = new List<ApplicationCommandProperties>();

        var triggers = GetChatTriggersFor(guildId);

        if (GetAcctErrors(triggers)?.Any() ?? false)
        {
            throw new InvalidOperationException("ACCTs cannot be build when ACCT errors are detected.");
        }

        if (triggers.Length == 0)
            return props;
        var groups = triggers.Where(x => x.ApplicationCommandType == CtApplicationCommandType.Slash
                                         && x.ValidTriggerTypes.HasFlag(ChatTriggerType.Interaction)
                                         && x.RealName.Split(' ').Length == 1)
            .Select(x => new TriggerChildGrouping(x.RealName, x, null)).ToList();
        triggers.Where(x =>
            x.ApplicationCommandType == CtApplicationCommandType.Slash && x.RealName.Split(' ').Length == 2).ForEach(
            x =>
            {
                if (groups.Any(y => y.Name == x.RealName.Split(' ').First()))
                    groups.First(y => y.Name == x.RealName.Split(' ').First()).Children
                        .Add(new(x.RealName.Split(' ').Last(), x, null));
                else
                    groups.Add(new(x.RealName.Split(' ').First(), null,
                        new List<TriggerChildGrouping>
                        {
                            new(x.RealName.Split(' ').Last(), x, null)
                        }));
            });

        triggers.Where(x =>
            x.ApplicationCommandType == CtApplicationCommandType.Slash
            && x.ValidTriggerTypes.HasFlag(ChatTriggerType.Interaction)
            && x.RealName.Split(' ').Length == 3).Select(x =>
        {
            TriggerChildGrouping group;
            if (groups.Any(y => y.Name == x.RealName.Split(' ').First()))
                group = groups.First(y => y.Name == x.RealName.Split(' ').First());
            else
            {
                groups.Add(new TriggerChildGrouping(x.RealName.Split(' ').First(), null, new List<TriggerChildGrouping>()));
                group = groups.First(y => y.Name == x.RealName.Split(' ').First());
            }

            return (Triggers: x, Group: group);
        }).Select(x =>
        {
            TriggerChildGrouping group;
            var groupChildren = x.Group.Children;
            if (groupChildren.Any(y => y.Name == x.Triggers.RealName.Split(' ')[1]))
                group = groupChildren.First(y => y.Name == x.Triggers.RealName.Split(' ')[1]);
            else
            {
                groupChildren.Add(new(x.Triggers.RealName.Split(' ')[1], null, new()));
                group = groupChildren.First(y => y.Name == x.Triggers.RealName.Split(' ')[1]);
            }

            return (x.Triggers, Group: group);
        }).ForEach(x => x.Group.Children.Add(new(x.Triggers.RealName, x.Triggers, null)));

        props = groups.Select(x => new SlashCommandBuilder()
            .WithName(x.Name)
            .WithDescription(x.Triggers?.ApplicationCommandDescription.IsNullOrWhiteSpace() ?? true
                ? "description"
                : x.Triggers!.ApplicationCommandDescription)
            .AddOptions(x.Triggers is not null
                ? Array.Empty<SlashCommandOptionBuilder>()
                : x.Children.Select(y => new SlashCommandOptionBuilder
                    {
                        Options = new()
                    }
                    .WithName(y.Name)
                    .WithDescription(y.Triggers?.ApplicationCommandDescription.IsNullOrWhiteSpace() ?? true
                        ? "description"
                        : y.Triggers!.ApplicationCommandDescription)
                    .WithType(y.Triggers is null
                        ? ApplicationCommandOptionType.SubCommandGroup
                        : ApplicationCommandOptionType.SubCommand)
                    .AddOptions(y.Children is null
                        ? Array.Empty<SlashCommandOptionBuilder>()
                        : y.Children.Select(z => new SlashCommandOptionBuilder()
                            .WithName(z.Name.Split(' ')[2])
                            .WithDescription(z.Triggers?.ApplicationCommandDescription.IsNullOrWhiteSpace() ?? true ? "description" : z.Triggers!.ApplicationCommandDescription)
                            .WithType(ApplicationCommandOptionType.SubCommand)).ToArray())).ToArray())).Select(x => x.Build() as ApplicationCommandProperties).ToList();

        triggers.Where(x => x.ApplicationCommandType == CtApplicationCommandType.Message).ForEach(x =>
            props.Add(new MessageCommandBuilder().WithName(x.RealName).WithDMPermission(false).Build()));

        triggers.Where(x => x.ApplicationCommandType == CtApplicationCommandType.User).ForEach(x =>
            props.Add(new UserCommandBuilder().WithName(x.RealName).WithDMPermission(false).Build()));
        return props;
    }

    public bool TryGetApplicationCommandProperties(ulong guildId, out List<ApplicationCommandProperties>? props)
    {
        try
        {
            props = GetApplicationCommandProperties(guildId);
            return true;
        }
        catch
        {
            props = null;
            return false;
        }
    }

    public async Task RegisterTriggersToGuildAsync(IGuild guild)
    {
        if (!TryGetApplicationCommandProperties(guild.Id, out var props) || props is null)
            return;
#if DEBUG
        var cmd = new List<IApplicationCommand>();
        foreach (var prop in props)
            cmd.Add(await guild.CreateApplicationCommandAsync(prop));
#else
        var cmd = await guild.BulkOverwriteApplicationCommandsAsync(props.ToArray()).ConfigureAwait(false);
        if (cmd is null) return;
#endif
        await using var uow = db.GetDbContext();
        var cts = uow.ChatTriggers.Where(x => x.GuildId == guild.Id).ToList();
        cmd.SelectMany(applicationCommand => applicationCommand.GetCtNames().Select(name => (cmd: applicationCommand, name))).ToList().ForEach(x =>
            cts.First(y => y.RealName == x.name).ApplicationCommandId = x.cmd.Id);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public static readonly Regex ValidCommandRegex = new(@"^(?:[\w-]{1,32} {0,1}){1,3}$", RegexOptions.Compiled);

    public static bool IsValidName(CtApplicationCommandType type, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length is > 32 or < 1)
            return false;

        return type is not CtApplicationCommandType.Slash || ValidCommandRegex.IsMatch(name);
    }

    public List<ChatTriggersInteractionError>? GetAcctErrors(ulong? guildId) =>
        GetAcctErrors(GetChatTriggersFor(guildId));

    public static List<ChatTriggersInteractionError>? GetAcctErrors(IEnumerable<CTModel> triggers)
    {
        triggers = triggers.Where(x => x.ApplicationCommandType != CtApplicationCommandType.None);
        var errors = new List<ChatTriggersInteractionError>();
        Dictionary<string?, List<(string Name, int Id)>> totalChildren = new();
        foreach (var trigger in triggers)
        {
            var triggerDepth = trigger.RealName.Split(' ').Length;
            var parent = triggerDepth > 1 ? trigger.RealName.Split(' ')[..^1].Join(' ') : "";
            if (!parent.IsNullOrWhiteSpace())
            {
                var value = totalChildren.GetValueOrDefault(parent, new());
                totalChildren[parent] = value.Append((trigger.RealName, trigger.Id)).ToList();
            }

            if (!IsValidName(trigger.ApplicationCommandType, trigger.RealName))
                errors.Add(new("invalid_name", new[]
                {
                    trigger.Id
                }, new[]
                {
                    trigger.RealName
                }));

            foreach (var newTrigger in triggers.Where(x => x.Id != trigger.Id))
            {
                var newTriggerDepth = trigger.RealName.Split(' ').Length;
                if (trigger.RealName == newTrigger.RealName)
                    errors.Add(new("duplicate", new[]
                        {
                            trigger.Id, newTrigger.Id
                        },
                        new[]
                        {
                            trigger.RealName, newTrigger.RealName
                        }));
                switch (triggerDepth)
                {
                    case 1 when newTriggerDepth == 2 && newTrigger.RealName.Split(' ')[0] == trigger.RealName:
                        errors.Add(new("subcommand_match_parent", new[]
                            {
                                trigger.Id, newTrigger.Id
                            },
                            new[]
                            {
                                trigger.RealName, newTrigger.RealName
                            }));
                        break;
                    case 2 when newTriggerDepth == 3 && newTrigger.RealName.Split(' ')[..1].Join(' ') == trigger.RealName:
                        errors.Add(new("subcommand_match_parent", new[]
                            {
                                trigger.Id, newTrigger.Id
                            },
                            new[]
                            {
                                trigger.RealName, newTrigger.RealName
                            }));
                        break;
                }
            }
        }

        totalChildren.Where(x => x.Value.Count > 25).ForEach(x => errors.Add(new("too_many_children",
            x.Value.Select(y => y.Id).ToArray(), x.Value.Select(y => y.Name).ToArray())));
        return errors.Any() ? errors : null;
    }
}
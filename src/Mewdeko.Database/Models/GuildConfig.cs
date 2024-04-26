﻿using System.ComponentModel.DataAnnotations.Schema;
using Mewdeko.Database.Common;

#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.

// ReSharper disable InconsistentNaming

namespace Mewdeko.Database.Models;

public class GuildConfig : DbEntity
{
    public ulong GuildId { get; set; }
    public string? Prefix { get; set; } = "";
    public ulong StaffRole { get; set; }
    public ulong GameMasterRole { get; set; }
    public ulong CommandLogChannel { get; set; } = 0;
    public bool DeleteMessageOnCommand { get; set; } = false;
    public string? WarnMessage { get; set; } = "-";
    public HashSet<DelMsgOnCmdChannel> DelMsgOnCmdChannels { get; set; } = new();
    public string? AutoAssignRoleId { get; set; } = "0";

    public string XpImgUrl { get; set; }
    public bool StatsOptOut { get; set; } = false;
    public string CurrencyName { get; set; } = "Coins";

    public string CurrencyEmoji { get; set; } = "💰";
    public int RewardAmount { get; set; } = 200;
    public int RewardTimeoutSeconds { get; set; } = 86400;

    public string GiveawayBanner { get; set; } = "";
    public string GiveawayEmbedColor { get; set; } = "";
    public string GiveawayWinEmbedColor { get; set; } = "";
    public bool DmOnGiveawayWin { get; set; } = true;
    public string GiveawayEndMessage { get; set; } = "";
    public ulong GiveawayPingRole { get; set; } = 0;

    // Starboard
    public bool StarboardAllowBots { get; set; } = true;
    public bool StarboardRemoveOnDelete { get; set; } = false;
    public bool StarboardRemoveOnReactionsClear { get; set; } = false;
    public bool StarboardRemoveOnBelowThreshold { get; set; } = true;
    public bool UseStarboardBlacklist { get; set; } = true;
    public string? StarboardCheckChannels { get; set; } = "0";

    // Votes
    public string? VotesPassword { get; set; }
    public ulong VotesChannel { get; set; }
    public string? VoteEmbed { get; set; }

    // Suggestions
    public int SuggestionThreadType { get; set; } = 0;
    public bool ArchiveOnDeny { get; set; } = false;
    public bool ArchiveOnAccept { get; set; } = false;
    public bool ArchiveOnConsider { get; set; } = false;
    public bool ArchiveOnImplement { get; set; } = false;
    public string? SuggestButtonMessage { get; set; } = "-";
    public string? SuggestButtonName { get; set; } = "-";
    public string? SuggestButtonEmote { get; set; } = "-";
    public int ButtonRepostThreshold { get; set; } = 5;
    public int SuggestCommandsType { get; set; } = 0;
    public ulong AcceptChannel { get; set; } = 0;
    public ulong DenyChannel { get; set; } = 0;
    public ulong ConsiderChannel { get; set; } = 0;
    public ulong ImplementChannel { get; set; } = 0;
    public int EmoteMode { get; set; } = 0;
    public string? SuggestMessage { get; set; } = "";
    public string? DenyMessage { get; set; } = "";
    public string? AcceptMessage { get; set; } = "";
    public string? ImplementMessage { get; set; } = "";
    public string? ConsiderMessage { get; set; } = "";
    public int MinSuggestLength { get; set; } = 0;
    public int MaxSuggestLength { get; set; } = 4098;
    public string? SuggestEmotes { get; set; }
    public ulong sugnum { get; set; } = 1;
    public ulong sugchan { get; set; }
    public ulong SuggestButtonChannel { get; set; } = 0;
    public int Emote1Style { get; set; } = 2;
    public int Emote2Style { get; set; } = 2;
    public int Emote3Style { get; set; } = 2;
    public int Emote4Style { get; set; } = 2;
    public int Emote5Style { get; set; } = 2;
    public ulong SuggestButtonMessageId { get; set; } = 0;
    public int SuggestButtonRepostThreshold { get; set; } = 5;
    public int SuggestButtonColor { get; set; } = 2;

    // Guild Currency Config
    // public string? CurrencyEmote { get; set; }
    // public string? CurrencyName { get; set; }
    // public ulong MinBet { get; set; }
    // public ulong MaxBet { get; set; }
    // public bool CurrencyGenerationEnabled { get; set; }
    // public int CurrencyGenerationMaxAmount { get; set; }
    // public int CurrencyGenerationMinAmount { get; set; }
    // public int CurrencyGenerationCooldown { get; set; }
    // public int CurrencyGenerationChance { get; set; }
    // public int TimelyCurrencyAmount { get; set; }
    // public int TimelyCurrencyCooldown { get; set; }

    public string? AfkMessage { get; set; } = "-";
    public string? AutoBotRoleIds { get; set; }
    public int GBEnabled { get; set; } = 1;
    public bool GBAction { get; set; } = false;
    public ulong ConfessionLogChannel { get; set; } = 0;
    public ulong ConfessionChannel { get; set; } = 0;
    public string? ConfessionBlacklist { get; set; } = "0";
    public int MultiGreetType { get; set; } = 0;
    public ulong MemberRole { get; set; } = 0;
    public string? TOpenMessage { get; set; } = "none";
    public string? GStartMessage { get; set; } = "none";
    public string? GEndMessage { get; set; } = "none";
    public string? GWinMessage { get; set; } = "none";
    public ulong WarnlogChannelId { get; set; } = 0;
    public ulong MiniWarnlogChannelId { get; set; } = 0;
    public bool SendBoostMessage { get; set; } = false;
    public string? GRolesBlacklist { get; set; } = "-";
    public string? GUsersBlacklist { get; set; } = "-";
    public string? BoostMessage { get; set; } = "%user% just boosted this server!";
    public ulong BoostMessageChannelId { get; set; }
    public int BoostMessageDeleteAfter { get; set; }
    public string? GiveawayEmote { get; set; } = "🎉";
    public ulong TicketChannel { get; set; } = 0;
    public ulong TicketCategory { get; set; } = 0;
    public bool snipeset { get; set; } = false;
    public int AfkLength { get; set; } = 250;
    public int XpTxtTimeout { get; set; }
    public int XpTxtRate { get; set; }
    public int XpVoiceRate { get; set; }
    public int XpVoiceTimeout { get; set; }
    public List<WarningPunishment2> WarnPunishments2 { get; set; } = new();
    public int Stars { get; set; } = 3;
    public int AfkType { get; set; } = 2;
    public AntiAltSetting AntiAltSetting { get; set; }
    public string? AfkDisabledChannels { get; set; }
    public string? AfkDel { get; set; }
    public int AfkTimeout { get; set; } = 20;
    public ulong Joins { get; set; }
    public ulong Leaves { get; set; }
    public string? Star2 { get; set; } = "⭐";
    public ulong StarboardChannel { get; set; }
    public int RepostThreshold { get; set; }
    public int PreviewLinks { get; set; }
    public ulong ReactChannel { get; set; }
    public int fwarn { get; set; }
    public int invwarn { get; set; }
    public int removeroles { get; set; }
    public bool AutoDeleteGreetMessages { get; set; } = false;
    public bool AutoDeleteByeMessages { get; set; } = false;
    public int AutoDeleteGreetMessagesTimer { get; set; } = 30;
    public int AutoDeleteByeMessagesTimer { get; set; } = 30;

    public ulong GreetMessageChannelId { get; set; }
    public ulong ByeMessageChannelId { get; set; }
    public string? GreetHook { get; set; } = "";
    public string? LeaveHook { get; set; } = "";

    public bool SendDmGreetMessage { get; set; } = false;
    public string? DmGreetMessageText { get; set; } = "Welcome to the %server% server, %user%!";

    public bool SendChannelGreetMessage { get; set; } = false;
    public string? ChannelGreetMessageText { get; set; } = "Welcome to the %server% server, %user%!";

    public bool SendChannelByeMessage { get; set; } = false;
    public string? ChannelByeMessageText { get; set; } = "%user% has left!";

    public LogSetting LogSetting { get; set; } = new();
    public bool ExclusiveSelfAssignedRoles { get; set; } = false;
    public bool AutoDeleteSelfAssignedRoleMessages { get; set; } = false;

    [ForeignKey("LogSettingId")]
    public int? LogSettingId { get; set; }

    //stream notifications
    public HashSet<FollowedStream> FollowedStreams { get; set; } = new();

    //permissions
    public List<Permissionv2> Permissions { get; set; }
    public bool VerbosePermissions { get; set; } = true;
    public string? PermissionRole { get; set; } = null;

    public HashSet<CommandCooldown> CommandCooldowns { get; set; } = new();

    //filtering
    public bool FilterInvites { get; set; } = false;
    public bool FilterLinks { get; set; } = false;
    public HashSet<FilterInvitesChannelIds> FilterInvitesChannelIds { get; set; } = new();
    public HashSet<FilterLinksChannelId> FilterLinksChannelIds { get; set; } = new();


    public bool FilterWords { get; set; } = false;
    public HashSet<FilteredWord> FilteredWords { get; set; } = new();
    public HashSet<FilterWordsChannelIds> FilterWordsChannelIds { get; set; } = new();

    public HashSet<MutedUserId> MutedUsers { get; set; } = new();

    public string? MuteRoleName { get; set; }
    public ulong CleverbotChannel { get; set; }
    public List<Repeater> GuildRepeaters { get; set; } = new();

    public AntiRaidSetting AntiRaidSetting { get; set; }
    public AntiSpamSetting AntiSpamSetting { get; set; }

    public string? Locale { get; set; } = null;
    public string? TimeZoneId { get; set; } = null;

    public HashSet<UnmuteTimer> UnmuteTimers { get; set; } = new();
    public HashSet<UnbanTimer> UnbanTimer { get; set; } = new();
    public HashSet<UnroleTimer> UnroleTimer { get; set; } = new();
    public HashSet<VcRoleInfo> VcRoleInfos { get; set; }
    public HashSet<CommandAlias> CommandAliases { get; set; } = new();
    public List<WarningPunishment> WarnPunishments { get; set; } = new();
    public bool WarningsInitialized { get; set; } = false;
    public HashSet<NsfwBlacklitedTag> NsfwBlacklistedTags { get; set; } = new();

    public ulong? GameVoiceChannel { get; set; } = null;
    public bool VerboseErrors { get; set; } = true;

    public StreamRoleSettings StreamRole { get; set; }

    public XpSettings? XpSettings { get; set; }
    public List<FeedSub> FeedSubs { get; set; } = new();
    public IndexedCollection<ReactionRoleMessage> ReactionRoleMessages { get; set; } = new();
    public bool NotifyStreamOffline { get; set; } = false;
    public List<GroupName> SelfAssignableRoleGroupNames { get; set; }
    public int WarnExpireHours { get; set; } = 0;
    public WarnExpireAction WarnExpireAction { get; set; } = WarnExpireAction.Clear;

    public uint JoinGraphColor { get; set; } = 4294956800;
    public uint LeaveGraphColor { get; set; } = 4294956800;
}
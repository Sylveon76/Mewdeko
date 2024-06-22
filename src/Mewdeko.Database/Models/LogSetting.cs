﻿namespace Mewdeko.Database.Models;

public class LogSetting : DbEntity
{
    public HashSet<IgnoredLogChannel> IgnoredChannels { get; set; } = new();

    public ulong? LogOtherId { get; set; } = 0;
    public ulong? MessageUpdatedId { get; set; } = 0;

    public ulong? MessageDeletedId { get; set; } = 0;

    // Threads
    public ulong? ThreadCreatedId { get; set; } = 0;
    public ulong? ThreadDeletedId { get; set; } = 0;

    public ulong? ThreadUpdatedId { get; set; } = 0;

    // Users
    public ulong? UsernameUpdatedId { get; set; } = 0;
    public ulong? NicknameUpdatedId { get; set; } = 0;
    public ulong? AvatarUpdatedId { get; set; } = 0;
    public ulong? UserLeftId { get; set; } = 0;
    public ulong? UserBannedId { get; set; } = 0;
    public ulong? UserUnbannedId { get; set; } = 0;
    public ulong? UserUpdatedId { get; set; } = 0;
    public ulong? UserJoinedId { get; set; } = 0;
    public ulong? UserRoleAddedId { get; set; } = 0;
    public ulong? UserRoleRemovedId { get; set; } = 0;
    public ulong? UserMutedId { get; set; } = 0;
    public ulong? LogUserPresenceId { get; set; } = 0;

    public ulong? LogVoicePresenceId { get; set; } = 0;

    // ReSharper disable once InconsistentNaming
    public ulong? LogVoicePresenceTTSId { get; set; } = 0;

    // Server
    public ulong? ServerUpdatedId { get; set; } = 0;
    public ulong? RoleUpdatedId { get; set; } = 0;
    public ulong? RoleDeletedId { get; set; } = 0;
    public ulong? EventCreatedId { get; set; } = 0;
    public ulong? RoleCreatedId { get; set; } = 0;

    // Channels
    public ulong? ChannelCreatedId { get; set; } = 0;
    public ulong? ChannelDestroyedId { get; set; } = 0;
    public ulong? ChannelUpdatedId { get; set; } = 0;


    //-------------------DO NOT USE----------------
    // Fuck you nadeko for making it like this
    /// <summary>
    /// DON'T USE
    /// </summary>
    public long IsLogging { get; set; } = 1;

    /// <summary>
    /// DON'T USE
    /// </summary>
    public ulong ChannelId { get; set; } = 0;

    /// <summary>
    /// DON'T USE
    /// </summary>
    public long MessageUpdated { get; set; } = 1;

    /// <summary>
    /// DON'T USE
    /// </summary>
    public long MessageDeleted { get; set; } = 1;

    /// <summary>
    /// DON'T USE
    /// </summary>
    public long UserJoined { get; set; } = 1;

    /// <summary>
    /// DON'T USE
    /// </summary>
    public long UserLeft { get; set; } = 1;

    /// <summary>
    /// DON'T USE
    /// </summary>
    public long UserBanned { get; set; } = 1;

    /// <summary>
    /// DON'T USE
    /// </summary>
    public long UserUnbanned { get; set; } = 1;

    /// <summary>
    /// DON'T USE
    /// </summary>
    public long UserUpdated { get; set; } = 1;

    /// <summary>
    /// DON'T USE
    /// </summary>
    public long ChannelCreated { get; set; } = 1;

    /// <summary>
    /// DON'T USE
    /// </summary>
    public long ChannelDestroyed { get; set; } = 1;

    /// <summary>
    /// DON'T USE
    /// </summary>
    public long ChannelUpdated { get; set; } = 1;

    /// <summary>
    /// DON'T USE
    /// </summary>
    public long LogUserPresence { get; set; } = 0;

    /// <summary>
    /// DON'T USE
    /// </summary>
    public ulong UserPresenceChannelId { get; set; } = 0;

    /// <summary>
    /// DON'T USE
    /// </summary>
    public long LogVoicePresence { get; set; } = 0;

    /// <summary>
    /// DON'T USE
    /// </summary>
    public ulong VoicePresenceChannelId { get; set; } = 0;
}
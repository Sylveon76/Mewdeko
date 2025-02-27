using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

/// <summary>
///     Server configuration for the custom voice channel system.
/// </summary>
[Table("CustomVoiceConfig")]
public class CustomVoiceConfig : DbEntity
{
    /// <summary>
    ///     Gets or sets the guild ID this configuration belongs to.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the voice channel that serves as the creation hub.
    /// </summary>
    public ulong HubVoiceChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the category ID where custom channels will be created.
    /// </summary>
    public ulong? ChannelCategoryId { get; set; }

    /// <summary>
    ///     Gets or sets the default name format for created channels.
    ///     {0} = User's name
    ///     {1} = User's discriminator
    ///     {2} = Guild name
    /// </summary>
    public string DefaultNameFormat { get; set; } = "{0}'s Channel";

    /// <summary>
    ///     Gets or sets the default channel user limit (0 = unlimited).
    /// </summary>
    public int DefaultUserLimit { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the default bitrate (in kbps) for created channels.
    /// </summary>
    public int DefaultBitrate { get; set; } = 64;

    /// <summary>
    ///     Gets or sets a value indicating whether channels should be automatically deleted when empty.
    /// </summary>
    public bool DeleteWhenEmpty { get; set; } = true;

    /// <summary>
    ///     Gets or sets the time in minutes a channel should remain empty before being deleted (0 = immediately).
    /// </summary>
    public int EmptyChannelTimeout { get; set; } = 1;

    /// <summary>
    ///     Gets or sets a value indicating whether users can create more than one channel.
    /// </summary>
    public bool AllowMultipleChannels { get; set; } = false;

    /// <summary>
    ///     Gets or sets a value indicating whether users can customize the name of their channel.
    /// </summary>
    public bool AllowNameCustomization { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether users can set user limits for their channel.
    /// </summary>
    public bool AllowUserLimitCustomization { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether users can set bitrate for their channel.
    /// </summary>
    public bool AllowBitrateCustomization { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether users can lock their channel.
    /// </summary>
    public bool AllowLocking { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether users can add/remove specific users from their channel.
    /// </summary>
    public bool AllowUserManagement { get; set; } = true;

    /// <summary>
    ///     Gets or sets the maximum user limit that users can set (0 = no maximum).
    /// </summary>
    public int MaxUserLimit { get; set; } = 99;

    /// <summary>
    ///     Gets or sets the maximum bitrate that users can set in kbps (0 = no maximum).
    /// </summary>
    public int MaxBitrate { get; set; } = 96;

    /// <summary>
    ///     Gets or sets a value indicating whether to persist user preferences.
    /// </summary>
    public bool PersistUserPreferences { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether channel creators should be given auto-permission.
    /// </summary>
    public bool AutoPermission { get; set; } = true;

    /// <summary>
    ///     Gets or sets the role ID that will grant access to all custom voice management.
    /// </summary>
    public ulong? CustomVoiceAdminRoleId { get; set; }
}

/// <summary>
///     Represents an active custom voice channel.
/// </summary>
[Table("CustomVoiceChannel")]
public class CustomVoiceChannel : DbEntity
{
    /// <summary>
    ///     Gets or sets the guild ID this channel belongs to.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the voice channel ID.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the user who owns this channel.
    /// </summary>
    public ulong OwnerId { get; set; }

    /// <summary>
    ///     Gets or sets the time when the channel was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets or sets the time when the channel was last active.
    /// </summary>
    public DateTime LastActive { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets or sets a value indicating whether the channel is currently locked.
    /// </summary>
    public bool IsLocked { get; set; } = false;

    /// <summary>
    ///     Gets or sets a value indicating whether the channel should be kept even when empty.
    /// </summary>
    public bool KeepAlive { get; set; } = false;

    /// <summary>
    ///     Gets or sets a JSON string of allowed users.
    /// </summary>
    public string? AllowedUsersJson { get; set; }

    /// <summary>
    ///     Gets or sets a JSON string of denied users.
    /// </summary>
    public string? DeniedUsersJson { get; set; }

    /// <summary>
    ///     Gets or sets a list of users explicitly allowed to join this channel.
    /// </summary>
    [NotMapped]
    public List<ulong> AllowedUsers { get; set; } = new();

    /// <summary>
    ///     Gets or sets a list of users explicitly denied from joining this channel.
    /// </summary>
    [NotMapped]
    public List<ulong> DeniedUsers { get; set; } = new();
}

/// <summary>
///     Represents a user's persistent voice channel preferences.
/// </summary>
[Table("UserVoicePreference")]
public class UserVoicePreference : DbEntity
{
    /// <summary>
    ///     Gets or sets the guild ID these preferences belong to.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the user ID these preferences belong to.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the custom name format preferred by this user.
    /// </summary>
    public string? NameFormat { get; set; }

    /// <summary>
    ///     Gets or sets the preferred user limit.
    /// </summary>
    public int? UserLimit { get; set; }

    /// <summary>
    ///     Gets or sets the preferred bitrate in kbps.
    /// </summary>
    public int? Bitrate { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the user prefers a locked channel.
    /// </summary>
    public bool? PreferLocked { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the user prefers their channel to be kept alive.
    /// </summary>
    public bool? KeepAlive { get; set; }

    /// <summary>
    ///     Gets or sets the user's whitelist JSON string.
    /// </summary>
    public string? WhitelistJson { get; set; }

    /// <summary>
    ///     Gets or sets the user's blacklist JSON string.
    /// </summary>
    public string? BlacklistJson { get; set; }
}
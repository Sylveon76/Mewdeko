namespace Mewdeko.Database.Models;

/// <summary>
///     Represents the settings for the music player.
/// </summary>
public class MusicPlayerSettings
{
    /// <summary>
    ///     Auto-generated ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the queue repeat type.
    /// </summary>
    public PlayerRepeatType PlayerRepeat { get; set; } = PlayerRepeatType.Queue;

    /// <summary>
    ///     Gets or sets the channel ID for music-related messages.
    /// </summary>
    public ulong? MusicChannelId { get; set; } = null;

    /// <summary>
    ///     Gets or sets the default volume for the player.
    /// </summary>
    public int Volume { get; set; } = 100;

    /// <summary>
    ///     Gets or sets the role ID that has DJ permissions.
    ///     If null, no DJ role is set.
    /// </summary>
    public ulong? DjRoleId { get; set; } = null;

    /// <summary>
    ///     Gets or sets the auto disconnect setting.
    /// </summary>
    public AutoDisconnect AutoDisconnect { get; set; } = AutoDisconnect.Voice;

    /// <summary>
    ///     Gets or sets the auto play setting.
    /// </summary>
    public int AutoPlay { get; set; } = 0;

    /// <summary>
    ///     Gets or sets whether vote skip is enabled.
    /// </summary>
    public bool VoteSkipEnabled { get; set; } = false;

    /// <summary>
    ///     Gets or sets the vote skip threshold percentage (1-100).
    /// </summary>
    public int VoteSkipThreshold { get; set; } = 50;
}

/// <summary>
///     Specifies the auto disconnect options.
/// </summary>
public enum AutoDisconnect
{
    /// <summary>
    ///     No auto disconnect.
    /// </summary>
    None,

    /// <summary>
    ///     Auto disconnect based on voice activity.
    /// </summary>
    Voice,

    /// <summary>
    ///     Auto disconnect when the queue is empty.
    /// </summary>
    Queue,

    /// <summary>
    ///     Auto disconnect based on either voice activity or an empty queue.
    /// </summary>
    Either
}

/// <summary>
///     Specifies the player repeat type.
/// </summary>
public enum PlayerRepeatType
{
    /// <summary>
    ///     No repeat.
    /// </summary>
    None,

    /// <summary>
    ///     Repeat the current track.
    /// </summary>
    Track,

    /// <summary>
    ///     Repeat the entire queue.
    /// </summary>
    Queue,

    /// <summary>
    ///     Repeat the current song.
    /// </summary>
    Song = 1,

    /// <summary>
    ///     Repeat all tracks.
    /// </summary>
    All = 2,

    /// <summary>
    ///     Turn off repeat.
    /// </summary>
    Off = 0
}
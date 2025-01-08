using System.ComponentModel.DataAnnotations;

namespace Mewdeko.Database.Models;

/// <summary>
///     Represents an aggregated count of messages for a specific user in a Discord channel.
///     Tracks both the total message count and maintains a collection of individual message timestamps.
///     This allows for detailed message activity tracking and statistical analysis.
/// </summary>
public class MessageCount
{
    /// <summary>
    ///     Gets or sets the unique identifier for the message count record.
    /// </summary>
    [Key]
    public long Id { get; set; }

    /// <summary>
    ///     Gets or sets the UTC timestamp when this message count record was created.
    ///     Defaults to the current UTC time when a new record is created.
    /// </summary>
    public DateTime? DateAdded { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets or sets the Discord ID of the guild where the messages were sent.
    ///     Used for guild-specific message tracking and statistics.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the Discord ID of the channel where the messages were sent.
    ///     Used for channel-specific message tracking and statistics.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the Discord ID of the user who sent the messages.
    ///     Used for user-specific message tracking and statistics.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the total number of messages sent by the user in this channel.
    ///     This is an aggregated count that increases with each message.
    /// </summary>
    public ulong Count { get; set; }

    /// <summary>
    ///     Gets or sets the collection of individual message timestamps associated with this count.
    ///     Provides detailed timing information for each message that contributed to the total count.
    /// </summary>
    public ICollection<MessageTimestamp> Timestamps { get; set; }
}
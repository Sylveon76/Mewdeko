using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

/// <summary>
///     Represents a timestamp entry for a message sent in a Discord guild.
///     Each entry tracks when a specific user sent a message in a specific channel.
///     This allows for detailed message activity analysis and statistics.
/// </summary>
public class MessageTimestamp
{
    /// <summary>
    ///     Gets or sets the unique identifier for the timestamp entry.
    /// </summary>
    [Key]
    public long Id { get; set; }

    /// <summary>
    ///     Gets or sets the Discord ID of the guild where the message was sent.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the Discord ID of the channel where the message was sent.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the Discord ID of the user who sent the message.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the exact UTC timestamp when the message was sent.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    ///     Gets or sets the foreign key reference to the associated MessageCount record.
    ///     This links the timestamp to its corresponding message count aggregation.
    /// </summary>
    [ForeignKey("MessageCount")]
    public long MessageCountId { get; set; }

    /// <summary>
    ///     Gets or sets the navigation property to the associated MessageCount record.
    ///     This provides direct access to the message count data this timestamp belongs to.
    /// </summary>
    public MessageCount MessageCount { get; set; }
}
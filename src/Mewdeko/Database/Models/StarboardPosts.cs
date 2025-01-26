using LinqToDB.Mapping;

namespace Mewdeko.Database.Models;

/// <summary>
///     Represents a message that has been posted to a starboard.
/// </summary>
[Table("StarboardPost")]
public class StarboardPost : DbEntity
{
    /// <summary>
    ///     Gets or sets the ID of the original message.
    /// </summary>
    public ulong MessageId { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the starboard post message.
    /// </summary>
    public ulong PostId { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the starboard configuration this post belongs to.
    /// </summary>
    [NewProperty]
    public int StarboardConfigId { get; set; }
}
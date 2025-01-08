using LinqToDB.Mapping;

namespace Mewdeko.Database.Models;

/// <summary>
///     Represents an AI conversation.
/// </summary>
[Table("AIConversation")]
public class AiConversation : DbEntity
{
    /// <summary>
    ///     Gets or sets the guild ID where this conversation takes place.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the user ID who owns this conversation.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the collection of messages in this conversation.
    /// </summary>
    public List<AiMessage> Messages { get; set; } = new();
}
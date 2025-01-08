using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

/// <summary>
///     Represents a message in an AI conversation.
/// </summary>
[LinqToDB.Mapping.Table("AIMessage")]
public class AiMessage : DbEntity
{
    /// <summary>
    ///     Gets or sets the role of the message sender.
    /// </summary>
    public string Role { get; set; }

    /// <summary>
    ///     Gets or sets the content of the message.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the conversation this message belongs to.
    /// </summary>
    public int ConversationId { get; set; }

    /// <summary>
    ///     Gets or sets the associated conversation.
    /// </summary>
    [ForeignKey(nameof(ConversationId))]
    public AiConversation Conversation { get; set; }
}
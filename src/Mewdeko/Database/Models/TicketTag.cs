namespace Mewdeko.Database.Models;

/// <summary>
/// Represents a ticket tag for categorization.
/// </summary>
public class TicketTag : DbEntity
{
    /// <summary>
    /// Gets or sets the ID of the guild this tag belongs to.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier string for this tag.
    /// </summary>
    public string TagId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the tag.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description of the tag.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the color associated with this tag.
    /// </summary>
    public uint Color { get; set; }
}
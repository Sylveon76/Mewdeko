namespace Mewdeko.Database.Models;

/// <summary>
/// Represents a ticket priority level.
/// </summary>
public class TicketPriority : DbEntity
{

    /// <summary>
    /// Gets or sets the ID of the guild this priority belongs to.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier string for this priority.
    /// </summary>
    public string PriorityId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the priority.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the emoji associated with this priority.
    /// </summary>
    public string Emoji { get; set; }

    /// <summary>
    /// Gets or sets the priority level (1-5, where 5 is highest).
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Gets or sets whether staff should be pinged for tickets with this priority.
    /// </summary>
    public bool PingStaff { get; set; }

    /// <summary>
    /// Gets or sets the required response time for tickets with this priority.
    /// </summary>
    public TimeSpan ResponseTime { get; set; }

    /// <summary>
    /// Gets or sets the color associated with this priority.
    /// </summary>
    public uint Color { get; set; }
}
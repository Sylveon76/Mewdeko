namespace Mewdeko.Database.Models;

/// <summary>
/// Represents a bot instance running on the local machine.
/// Each instance is identified by its unique port number.
/// </summary>
public class BotInstance : DbEntity
{
    /// <summary>
    /// The TCP port number the bot instance is listening on.
    /// Must be between 1024 and 65535.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// The unique identifier of the bot instance.
    /// </summary>
    public ulong BotId { get; set; }

    /// <summary>
    /// The display name of the bot instance.
    /// </summary>
    public string BotName { get; set; }

    /// <summary>
    /// URL to the bot's avatar image.
    /// </summary>
    public string BotAvatar { get; set; }

    /// <summary>
    /// Indicates whether the bot instance is currently running and responsive.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// The timestamp of the last successful status check.
    /// </summary>
    public DateTime LastStatusUpdate { get; set; }
}
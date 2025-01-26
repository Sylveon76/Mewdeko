using LinqToDB.Mapping;

namespace Mewdeko.Database.Models;

/// <summary>
///     Represents the configuration for a starboard in a guild.
/// </summary>
[Table("Starboards")]
public class StarboardConfig : DbEntity
{
    /// <summary>
    ///     Gets or sets the ID of the guild this starboard belongs to.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the channel ID where starred messages will be posted.
    /// </summary>
    public ulong StarboardChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the emote used to trigger this starboard.
    /// </summary>
    public string Emote { get; set; }

    /// <summary>
    ///     Gets or sets the number of reactions required to add a message to the starboard.
    /// </summary>
    public int Threshold { get; set; }

    /// <summary>
    ///     Gets or sets the channels being checked for this starboard configuration.
    /// </summary>
    public string CheckedChannels { get; set; } = "";

    /// <summary>
    ///     Gets or sets a value indicating whether to use blacklist mode for checking channels.
    /// </summary>
    public bool UseBlacklist { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether bot messages can be starred.
    /// </summary>
    public bool AllowBots { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether to remove starboard posts when the original message is deleted.
    /// </summary>
    public bool RemoveOnDelete { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether to remove starboard posts when reactions are cleared.
    /// </summary>
    public bool RemoveOnReactionsClear { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether to remove starboard posts when they fall below the threshold.
    /// </summary>
    public bool RemoveOnBelowThreshold { get; set; }

    /// <summary>
    ///     Gets or sets the number of messages after which a starboard post should be reposted.
    /// </summary>
    public int RepostThreshold { get; set; }
}
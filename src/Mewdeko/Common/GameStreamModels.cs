using System.Text.Json.Serialization;

namespace Mewdeko.Common;

/// <summary>
///     Represents a game status update.
/// </summary>
public class GameStatus
{
    /// <summary>
    ///     Gets or sets the name of the game.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    ///     Gets or sets the type of activity.
    /// </summary>
    [JsonPropertyName("activity")]
    public ActivityType Activity { get; set; } = ActivityType.Playing;
}

/// <summary>
///     Represents a streaming status update.
/// </summary>
public class StreamStatus
{
    /// <summary>
    ///     Gets or sets the name of the stream.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    ///     Gets or sets the URL of the stream.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}
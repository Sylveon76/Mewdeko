using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Searches.Common;

/// <summary>
///     Represents a response from the joke API containing a setup and punchline.
/// </summary>
public class JokeResponse
{
    /// <summary>
    ///     Gets or sets the setup portion of the joke.
    /// </summary>
    /// <value>The setup text that begins the joke.</value>
    [JsonPropertyName("setup")]
    public string? Setup { get; set; }

    /// <summary>
    ///     Gets or sets the punchline portion of the joke.
    /// </summary>
    /// <value>The punchline text that concludes the joke.</value>
    [JsonPropertyName("punchline")]
    public string? Punchline { get; set; }
}
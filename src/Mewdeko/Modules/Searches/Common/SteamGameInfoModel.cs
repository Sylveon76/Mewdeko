using System.Text.Json.Serialization;
using Mewdeko.Modules.Searches.Services;

namespace Mewdeko.Modules.Searches.Common;

/// <summary>
///     Represents detailed information about a Steam game.
/// </summary>
public class SteamGameInfo
{
    /// <summary>
    ///     Gets or sets the unique Steam App ID.
    /// </summary>
    [JsonPropertyName("appid")]
    public int AppId { get; set; }

    /// <summary>
    ///     Gets or sets the name of the game.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets whether the game is free to play.
    /// </summary>
    [JsonPropertyName("is_free")]
    public bool IsFree { get; set; }

    /// <summary>
    ///     Gets or sets detailed description of the game.
    /// </summary>
    [JsonPropertyName("detailed_description")]
    public string? DetailedDescription { get; set; }

    /// <summary>
    ///     Gets or sets the short description of the game.
    /// </summary>
    [JsonPropertyName("short_description")]
    public string? ShortDescription { get; set; }

    /// <summary>
    ///     Gets or sets the header image URL.
    /// </summary>
    [JsonPropertyName("header_image")]
    public string? HeaderImage { get; set; }

    /// <summary>
    ///     Gets or sets the supported languages.
    /// </summary>
    [JsonPropertyName("supported_languages")]
    public string? SupportedLanguages { get; set; }

    /// <summary>
    ///     Gets or sets the release date information.
    /// </summary>
    [JsonPropertyName("release_date")]
    public ReleaseDate? ReleaseDate { get; set; }

    /// <summary>
    ///     Gets or sets the metacritic information.
    /// </summary>
    [JsonPropertyName("metacritic")]
    public Metacritic? Metacritic { get; set; }

    /// <summary>
    ///     Gets or sets the categories of the game.
    /// </summary>
    [JsonPropertyName("categories")]
    public List<Category>? Categories { get; set; }

    /// <summary>
    ///     Gets or sets the genres of the game.
    /// </summary>
    [JsonPropertyName("genres")]
    public List<Genre>? Genres { get; set; }
}

/// <summary>
///     Represents release date information.
/// </summary>
public class ReleaseDate
{
    /// <summary>
    ///     Gets or sets whether the release date is coming soon.
    /// </summary>
    [JsonPropertyName("coming_soon")]
    public bool ComingSoon { get; set; }

    /// <summary>
    ///     Gets or sets the release date.
    /// </summary>
    [JsonPropertyName("date")]
    public string? Date { get; set; }
}

/// <summary>
///     Represents metacritic score information.
/// </summary>
public class Metacritic
{
    /// <summary>
    ///     Gets or sets the score.
    /// </summary>
    [JsonPropertyName("score")]
    public int Score { get; set; }

    /// <summary>
    ///     Gets or sets the URL to the metacritic page.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

/// <summary>
///     Represents a game category.
/// </summary>
public class Category
{
    /// <summary>
    ///     Gets or sets the category ID.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the category description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
///     Represents a game genre.
/// </summary>
public class Genre
{
    /// <summary>
    ///     Gets or sets the genre ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    ///     Gets or sets the genre description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
///     Response structure for Steam App List API.
/// </summary>
public class SteamAppListResponse
{
    /// <summary>
    /// List of apps for this search
    /// </summary>
    [JsonPropertyName("applist")]
    public AppList? Applist { get; set; }
}

/// <summary>
///     Contains the list of Steam apps.
/// </summary>
public class AppList
{
    /// <summary>
    /// More apps list
    /// </summary>
    [JsonPropertyName("apps")]
    public List<SteamGameId>? Apps { get; set; }
}

/// <summary>
///     Response structure for Steam Game Details API.
/// </summary>
public class SteamGameDetailsResponse
{
    /// <summary>
    /// Whether getting data succeeded.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// The data for the searched game.
    /// </summary>
    [JsonPropertyName("data")]
    public SteamGameInfo? Data { get; set; }
}
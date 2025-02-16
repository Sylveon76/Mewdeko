using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Searches.Common;

/// <summary>
/// Represents the response from the Steam Store search API.
/// </summary>
public class StoreSearchResponse
{
    /// <summary>
    /// Gets or sets the total number of search results found.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Gets or sets the list of search result items.
    /// </summary>
    public List<StoreSearchItem> Items { get; set; } = new();
}

/// <summary>
/// Represents a single item in the Steam Store search results.
/// </summary>
public class StoreSearchItem
{
    /// <summary>
    /// Gets or sets the Steam Application ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the type of the item (e.g., "app", "dlc", etc.).
    /// </summary>
    public string Type { get; set; } = "";

    /// <summary>
    /// Gets or sets the name of the game or application.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the pricing information for the item.
    /// </summary>
    public PriceOverview? Price { get; set; }

    /// <summary>
    /// Gets or sets the Metacritic score of the game.
    /// </summary>
    public string Metascore { get; set; }

    /// <summary>
    /// Gets or sets the URL of the item's thumbnail image.
    /// </summary>
    public string TinyImage { get; set; }

    /// <summary>
    /// Gets or sets platform support information.
    /// </summary>
    public PlatformSupport Platforms { get; set; }

    /// <summary>
    /// Gets or sets the level of controller support (e.g., "full", "partial").
    /// </summary>
    public string ControllerSupport { get; set; }
}

/// <summary>
/// Represents pricing information for a Steam item.
/// </summary>
public class PriceOverview
{
    /// <summary>
    /// Gets or sets the currency code (e.g., "USD").
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the initial price in the smallest currency unit (e.g., cents).
    /// </summary>
    public int Initial { get; set; }

    /// <summary>
    /// Gets or sets the final price after discounts in the smallest currency unit.
    /// </summary>
    public int Final { get; set; }
}

/// <summary>
/// Represents platform compatibility information.
/// </summary>
public class PlatformSupport
{
    /// <summary>
    /// Gets or sets a value indicating whether the item is compatible with Windows.
    /// </summary>
    public bool Windows { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the item is compatible with macOS.
    /// </summary>
    public bool Mac { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the item is compatible with Linux.
    /// </summary>
    public bool Linux { get; set; }
}

/// <summary>
/// Represents the response from the Steam App Details API.
/// </summary>
public class AppDetailsResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the API request was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the detailed game information.
    /// </summary>
    public SteamGameInfo Data { get; set; }
}

/// <summary>
/// Represents detailed information about a Steam game.
/// </summary>
public class SteamGameInfo
{
    /// <summary>
    /// Gets or sets the type of the application (e.g., "game", "dlc").
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the name of the game.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the Steam Application ID.
    /// </summary>
    public int SteamAppid { get; set; }

    /// <summary>
    /// Gets or sets the detailed HTML description of the game.
    /// </summary>
    public string DetailedDescription { get; set; }

    /// <summary>
    /// Gets or sets the HTML formatted "About the Game" section.
    /// </summary>
    public string AboutTheGame { get; set; }

    /// <summary>
    /// Gets or sets a brief description of the game.
    /// </summary>
    public string ShortDescription { get; set; }

    /// <summary>
    /// Gets or sets the URL of the game's header image.
    /// </summary>
    public string HeaderImage { get; set; }

    /// <summary>
    /// Gets or sets the list of game developers.
    /// </summary>
    public List<string> Developers { get; set; }

    /// <summary>
    /// Gets or sets the list of game publishers.
    /// </summary>
    public List<string> Publishers { get; set; }

    /// <summary>
    /// Gets or sets the pricing information.
    /// </summary>
    public PriceOverview Price { get; set; }

    /// <summary>
    /// Gets or sets the game categories (e.g., "Single-player", "Multi-player").
    /// </summary>
    public List<Category> Categories { get; set; }

    /// <summary>
    /// Gets or sets the game genres.
    /// </summary>
    public List<Genre> Genres { get; set; }

    /// <summary>
    /// Gets or sets the game screenshots.
    /// </summary>
    public List<Screenshot>? Screenshots { get; set; }

    /// <summary>
    /// Gets or sets the platform compatibility information.
    /// </summary>
    public Dictionary<string, bool> Platforms { get; set; }

    /// <summary>
    /// Gets or sets the Metacritic information.
    /// </summary>
    public MetacriticInfo Metacritic { get; set; }

    /// <summary>
    /// Gets or sets the recommendation information.
    /// </summary>
    public Recommendations? Recommendations { get; set; }

    /// <summary>
    /// Gets or sets the release date information.
    /// </summary>
    public ReleaseDate ReleaseDate { get; set; }

    /// <summary>
    /// Gets or sets the game's website URL.
    /// </summary>
    public string Website { get; set; }

    /// <summary>
    /// Gets or sets the PC system requirements.
    /// </summary>
    public PCRequirements PcRequirements { get; set; }

    /// <summary>
    /// Gets or sets the support information URL.
    /// </summary>
    public string SupportInfo { get; set; }

    /// <summary>
    /// Gets or sets the background image URL.
    /// </summary>
    public string Background { get; set; }

    /// <summary>
    /// Gets or sets the Metacritic score.
    /// </summary>
    public string Metascore { get; set; }
}

/// <summary>
/// Represents a game category on Steam.
/// </summary>
public class Category
{
    /// <summary>
    /// Gets or sets the category ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the category description.
    /// </summary>
    public string Description { get; set; }
}

/// <summary>
/// Represents a game genre on Steam.
/// </summary>
public class Genre
{
    /// <summary>
    /// Gets or sets the genre ID.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the genre description.
    /// </summary>
    public string Description { get; set; }
}

/// <summary>
/// Represents a game screenshot.
/// </summary>
public class Screenshot
{
    /// <summary>
    /// Gets or sets the screenshot ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the thumbnail image path.
    /// </summary>
    [JsonPropertyName("path_thumbnail")]
    public string PathThumbnail { get; set; }

    /// <summary>
    /// Gets or sets the full-size image path.
    /// </summary>
    [JsonPropertyName("path_full")]
    public string PathFull { get; set; }
}

/// <summary>
/// Represents Metacritic review information.
/// </summary>
public class MetacriticInfo
{
    /// <summary>
    /// Gets or sets the Metacritic score.
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// Gets or sets the URL to the Metacritic review.
    /// </summary>
    public string Url { get; set; }
}

/// <summary>
/// Represents Steam user recommendations.
/// </summary>
public class Recommendations
{
    /// <summary>
    /// Gets or sets the total number of recommendations.
    /// </summary>
    public int Total { get; set; }
}

/// <summary>
/// Represents release date information.
/// </summary>
public class ReleaseDate
{
    /// <summary>
    /// Gets or sets a value indicating whether the game is coming soon.
    /// </summary>
    [JsonPropertyName("coming_soon")]
    public bool ComingSoon { get; set; }

    /// <summary>
    /// Gets or sets the release date string.
    /// </summary>
    public string Date { get; set; }
}

/// <summary>
/// Represents PC system requirements.
/// </summary>
public class PCRequirements
{
    /// <summary>
    /// Gets or sets the minimum system requirements in HTML format.
    /// </summary>
    public string Minimum { get; set; }

    /// <summary>
    /// Gets or sets the recommended system requirements in HTML format.
    /// </summary>
    public string Recommended { get; set; }
}
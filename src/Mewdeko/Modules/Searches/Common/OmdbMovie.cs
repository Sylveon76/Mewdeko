

using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Searches.Common;

/// <summary>
///     Represents movie information retrieved from Wikipedia.
/// </summary>
public class WikiMovie
{
    /// <summary>
    ///     Gets or sets the title of the movie.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    ///     Gets or sets the release year of the movie.
    /// </summary>
    public string Year { get; set; }

    /// <summary>
    ///     Gets or sets the plot summary of the movie.
    /// </summary>
    public string Plot { get; set; }

    /// <summary>
    ///     Gets or sets the full Wikipedia URL for the movie.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    ///     Gets or sets the URL of the movie's poster or main image.
    /// </summary>
    public string ImageUrl { get; set; }
}

/// <summary>
///     Represents the response from Wikipedia's search API.
/// </summary>
public class WikiSearchResponse
{
    /// <summary>
    ///     Gets or sets the query results from the Wikipedia search.
    /// </summary>
    [JsonPropertyName("query")]
    public WikiQuerySearch Query { get; set; }
}

/// <summary>
///     Represents the search section of a Wikipedia API query.
/// </summary>
public class WikiQuerySearch
{
    /// <summary>
    ///     Gets or sets the list of search results.
    /// </summary>
    [JsonPropertyName("search")]
    public List<WikiSearchResult> Search { get; set; }
}

/// <summary>
///     Represents a single search result from Wikipedia.
/// </summary>
public class WikiSearchResult
{
    /// <summary>
    ///     Gets or sets the unique page ID of the Wikipedia article.
    /// </summary>
    [JsonPropertyName("pageid")]
    public int PageId { get; set; }
}

/// <summary>
///     Represents the response from Wikipedia's content API.
/// </summary>
public class WikiContentResponse
{
    /// <summary>
    ///     Gets or sets the query results containing page content.
    /// </summary>
    [JsonPropertyName("query")]
    public WikiQueryContent Query { get; set; }
}

/// <summary>
///     Represents the content section of a Wikipedia API query.
/// </summary>
public class WikiQueryContent
{
    /// <summary>
    ///     Gets or sets the dictionary of page contents, keyed by page ID.
    /// </summary>
    [JsonPropertyName("pages")]
    public Dictionary<string, WikiPage> Pages { get; set; }
}

/// <summary>
///     Represents a Wikipedia page and its contents.
/// </summary>
public class WikiPage
{
    /// <summary>
    ///     Gets or sets the title of the Wikipedia page.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; }

    /// <summary>
    ///     Gets or sets the main text content of the Wikipedia page.
    /// </summary>
    [JsonPropertyName("extract")]
    public string Extract { get; set; }

    /// <summary>
    ///     Gets or sets the full URL of the Wikipedia page.
    /// </summary>
    [JsonPropertyName("fullurl")]
    public string FullUrl { get; set; }

    /// <summary>
    ///     Gets or sets the thumbnail image information for the page.
    /// </summary>
    [JsonPropertyName("thumbnail")]
    public WikiThumbnail Thumbnail { get; set; }
}

/// <summary>
///     Represents thumbnail image information from Wikipedia.
/// </summary>
public class WikiThumbnail
{
    /// <summary>
    ///     Gets or sets the URL of the thumbnail image.
    /// </summary>
    [JsonPropertyName("source")]
    public string Source { get; set; }
}
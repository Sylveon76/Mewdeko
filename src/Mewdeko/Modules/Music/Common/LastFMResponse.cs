

using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Music.Common;

/// <summary>
///     Represents an artist with details like name, MusicBrainz ID, and their URL on Last.fm.
/// </summary>
public class Artist
{
    /// <summary>
    ///     Gets or sets the MusicBrainz ID of the artist.
    /// </summary>
    [JsonPropertyName("mbid")] public string Mbid;

    /// <summary>
    ///     Gets or sets the name of the artist.
    /// </summary>
    [JsonPropertyName("name")] public string Name;

    /// <summary>
    ///     Gets or sets the URL of the Last.fm page for the artist.
    /// </summary>
    [JsonPropertyName("url")] public string Url;
}

/// <summary>
///     Attribute data related to the similar tracks response.
/// </summary>
public class Attr
{
    /// <summary>
    ///     Gets or sets the artist name associated with the similar tracks.
    /// </summary>
    [JsonPropertyName("artist")] public string Artist;
}

/// <summary>
///     Represents an image with a URL and its size.
/// </summary>
public class Image
{
    /// <summary>
    ///     Gets or sets the size of the image (e.g., small, medium, large).
    /// </summary>
    [JsonPropertyName("size")] public string Size;

    /// <summary>
    ///     Gets or sets the URL of the image.
    /// </summary>
    [JsonPropertyName("#text")] public string Text;
}

/// <summary>
///     The root response object for LastFM API calls returning similar tracks.
/// </summary>
public class LastFmResponse
{
    /// <summary>
    ///     Gets or sets the similar tracks returned by the API.
    /// </summary>
    [JsonPropertyName("similartracks")] public Similartracks? Similartracks;
}

/// <summary>
///     Contains a list of similar tracks and associated attributes.
/// </summary>
public class Similartracks
{
    /// <summary>
    ///     Gets or sets additional attributes for the similar tracks.
    /// </summary>
    [JsonPropertyName("@attr")] public Attr Attr;

    /// <summary>
    ///     Gets or sets the list of similar tracks.
    /// </summary>
    [JsonPropertyName("track")] public List<Track> Track;
}

/// <summary>
///     Represents the streamable status of a track.
/// </summary>
public class Streamable
{
    /// <summary>
    ///     Gets or sets the flag indicating if the full track is streamable.
    /// </summary>
    [JsonPropertyName("fulltrack")] public string Fulltrack;

    /// <summary>
    ///     Gets or sets the text indicating whether a track is streamable.
    /// </summary>
    [JsonPropertyName("#text")] public string Text;
}

/// <summary>
///     Represents a music track with various properties like name, play count, and URL.
/// </summary>
public class Track
{
    /// <summary>
    ///     Gets or sets the artist of the track.
    /// </summary>
    [JsonPropertyName("artist")] public Artist Artist;

    /// <summary>
    ///     Gets or sets the duration of the track in milliseconds.
    /// </summary>
    [JsonPropertyName("duration")] public int? Duration;

    /// <summary>
    ///     Gets or sets the list of images associated with the track.
    /// </summary>
    [JsonPropertyName("image")] public List<Image> Image;

    /// <summary>
    ///     Gets or sets the match score indicating the relevance of the track in the context of the search.
    /// </summary>
    [JsonPropertyName("match")] public double? Match;

    /// <summary>
    ///     Gets or sets the MusicBrainz ID of the track.
    /// </summary>
    [JsonPropertyName("mbid")] public string Mbid;

    /// <summary>
    ///     Gets or sets the name of the track.
    /// </summary>
    [JsonPropertyName("name")] public string Name;

    /// <summary>
    ///     Gets or sets the play count of the track.
    /// </summary>
    [JsonPropertyName("playcount")] public int? Playcount;

    /// <summary>
    ///     Gets or sets the streamable status of the track.
    /// </summary>
    [JsonPropertyName("streamable")] public Streamable Streamable;

    /// <summary>
    ///     Gets or sets the URL of the Last.fm page for the track.
    /// </summary>
    [JsonPropertyName("url")] public string Url;
}
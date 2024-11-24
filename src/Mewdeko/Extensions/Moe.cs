

using System.Text.Json.Serialization;

namespace Mewdeko.Extensions;

/// <summary>
///     Represents a result from the Moe API.
/// </summary>
public class Result
{
    /// <summary>
    ///     The Anilist ID.
    /// </summary>
    [JsonPropertyName("anilist")]
    public int Anilist { get; set; }

    /// <summary>
    ///     The filename of the image.
    /// </summary>
    [JsonPropertyName("filename")]
    public string Filename { get; set; }

    /// <summary>
    ///     The episode number.
    /// </summary>
    [JsonPropertyName("episode")]
    public double Episode { get; set; }

    /// <summary>
    ///     The time the scene starts.
    /// </summary>
    [JsonPropertyName("from")]
    public double From { get; set; }

    /// <summary>
    ///     The time the scene ends.
    /// </summary>
    [JsonPropertyName("to")]
    public double To { get; set; }

    /// <summary>
    ///     The similarity of the scene in percentage.
    /// </summary>
    [JsonPropertyName("similarity")]
    public double Similarity { get; set; }

    /// <summary>
    ///     The video URL.
    /// </summary>
    [JsonPropertyName("video")]
    public string Video { get; set; }

    /// <summary>
    ///     The image URL.
    /// </summary>
    [JsonPropertyName("image")]
    public string Image { get; set; }
}

/// <summary>
///     Represents a response from the Moe API.
/// </summary>
public class MoeResponse
{
    /// <summary>
    ///     The total number of frames.
    /// </summary>
    [JsonPropertyName("frameCount")]
    public int FrameCount { get; set; }

    /// <summary>
    ///     The error message.
    /// </summary>
    [JsonPropertyName("error")]
    public string Error { get; set; }

    /// <summary>
    ///     The results from the API.
    /// </summary>
    [JsonPropertyName("result")]
    public List<Result> Result { get; set; }
}
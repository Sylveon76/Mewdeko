

using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Searches.Common;

/// <summary>
///     Represents user statistics retrieved from the Gatari API.
/// </summary>
public class UserStats
{
    /// <summary>
    ///     Gets or sets the count of A ranks.
    /// </summary>
    [JsonPropertyName("a_count")]
    public int ACount { get; set; }

    /// <summary>
    ///     Gets or sets the average accuracy.
    /// </summary>
    [JsonPropertyName("avg_accuracy")]
    public double AvgAccuracy { get; set; }

    /// <summary>
    ///     Gets or sets the average hits per play.
    /// </summary>
    [JsonPropertyName("avg_hits_play")]
    public double AvgHitsPlay { get; set; }

    /// <summary>
    ///     Gets or sets the country rank.
    /// </summary>
    [JsonPropertyName("country_rank")]
    public int CountryRank { get; set; }

    /// <summary>
    ///     Gets or sets the user ID.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the user level.
    /// </summary>
    [JsonPropertyName("level")]
    public int Level { get; set; }

    /// <summary>
    ///     Gets or sets the level progress.
    /// </summary>
    [JsonPropertyName("level_progress")]
    public int LevelProgress { get; set; }

    /// <summary>
    ///     Gets or sets the maximum combo achieved.
    /// </summary>
    [JsonPropertyName("max_combo")]
    public int MaxCombo { get; set; }

    /// <summary>
    ///     Gets or sets the total play count.
    /// </summary>
    [JsonPropertyName("playcount")]
    public int Playcount { get; set; }

    /// <summary>
    ///     Gets or sets the total playtime.
    /// </summary>
    [JsonPropertyName("playtime")]
    public int Playtime { get; set; }

    /// <summary>
    ///     Gets or sets the performance points (PP).
    /// </summary>
    [JsonPropertyName("pp")]
    public int Pp { get; set; }

    /// <summary>
    ///     Gets or sets the overall rank.
    /// </summary>
    [JsonPropertyName("rank")]
    public int Rank { get; set; }

    /// <summary>
    ///     Gets or sets the ranked score.
    /// </summary>
    [JsonPropertyName("ranked_score")]
    public int RankedScore { get; set; }

    /// <summary>
    ///     Gets or sets the count of replays watched.
    /// </summary>
    [JsonPropertyName("replays_watched")]
    public int ReplaysWatched { get; set; }

    /// <summary>
    ///     Gets or sets the count of S ranks.
    /// </summary>
    [JsonPropertyName("s_count")]
    public int SCount { get; set; }

    /// <summary>
    ///     Gets or sets the count of SH ranks.
    /// </summary>
    [JsonPropertyName("sh_count")]
    public int ShCount { get; set; }

    /// <summary>
    ///     Gets or sets the total hits.
    /// </summary>
    [JsonPropertyName("total_hits")]
    public int TotalHits { get; set; }

    /// <summary>
    ///     Gets or sets the total score.
    /// </summary>
    [JsonPropertyName("total_score")]
    public long TotalScore { get; set; }

    /// <summary>
    ///     Gets or sets the count of X ranks.
    /// </summary>
    [JsonPropertyName("x_count")]
    public int XCount { get; set; }

    /// <summary>
    ///     Gets or sets the count of XH ranks.
    /// </summary>
    [JsonPropertyName("xh_count")]
    public int XhCount { get; set; }
}

/// <summary>
///     Represents the response structure for user statistics from the Gatari API.
/// </summary>
public class GatariUserStatsResponse
{
    /// <summary>
    ///     Gets or sets the response code.
    /// </summary>
    [JsonPropertyName("code")]
    public int Code { get; set; }

    /// <summary>
    ///     Gets or sets the user statistics.
    /// </summary>
    [JsonPropertyName("stats")]
    public UserStats Stats { get; set; }
}
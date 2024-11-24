

using System.Text.Json.Serialization;

namespace Mewdeko.Common;

/// <summary>
///     Represents a user's best performance on an Osu! map.
/// </summary>
public class OsuUserBests
{
    /// <summary>
    ///     Gets or sets the beatmap ID.
    /// </summary>
    [JsonPropertyName("beatmap_id")]
    public string BeatmapId { get; set; }

    /// <summary>
    ///     Gets or sets the score ID.
    /// </summary>
    [JsonPropertyName("score_id")]
    public string ScoreId { get; set; }

    /// <summary>
    ///     Gets or sets the score achieved.
    /// </summary>
    [JsonPropertyName("score")]
    public string Score { get; set; }

    /// <summary>
    ///     Gets or sets the maximum combo achieved.
    /// </summary>
    [JsonPropertyName("maxcombo")]
    public string Maxcombo { get; set; }

    /// <summary>
    ///     Gets or sets the number of 50s.
    /// </summary>
    [JsonPropertyName("count50")]
    public double Count50 { get; set; }

    /// <summary>
    ///     Gets or sets the number of 100s.
    /// </summary>
    [JsonPropertyName("count100")]
    public double Count100 { get; set; }

    /// <summary>
    ///     Gets or sets the number of 300s.
    /// </summary>
    [JsonPropertyName("count300")]
    public double Count300 { get; set; }

    /// <summary>
    ///     Gets or sets the number of misses.
    /// </summary>
    [JsonPropertyName("countmiss")]
    public int Countmiss { get; set; }

    /// <summary>
    ///     Gets or sets the number of katus.
    /// </summary>
    [JsonPropertyName("countkatu")]
    public double Countkatu { get; set; }

    /// <summary>
    ///     Gets or sets the number of gekis.
    /// </summary>
    [JsonPropertyName("countgeki")]
    public double Countgeki { get; set; }

    /// <summary>
    ///     Gets or sets whether the performance was perfect.
    /// </summary>
    [JsonPropertyName("perfect")]
    public string Perfect { get; set; }

    /// <summary>
    ///     Gets or sets the enabled mods.
    /// </summary>
    [JsonPropertyName("enabled_mods")]
    public int EnabledMods { get; set; }

    /// <summary>
    ///     Gets or sets the user ID.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string UserId { get; set; }

    /// <summary>
    ///     Gets or sets the date of the performance.
    /// </summary>
    [JsonPropertyName("date")]
    public string Date { get; set; }

    /// <summary>
    ///     Gets or sets the rank achieved.
    /// </summary>
    [JsonPropertyName("rank")]
    public string Rank { get; set; }

    /// <summary>
    ///     Gets or sets the performance points (pp) earned.
    /// </summary>
    [JsonPropertyName("pp")]
    public double Pp { get; set; }

    /// <summary>
    ///     Gets or sets whether the replay is available.
    /// </summary>
    [JsonPropertyName("replay_available")]
    public string ReplayAvailable { get; set; }
}
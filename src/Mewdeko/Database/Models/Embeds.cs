namespace Mewdeko.Database.Models;

/// <summary>
/// Embed Storage
/// </summary>
public class Embeds : DbEntity
{
    /// <summary>
    /// Name of the embed
    /// </summary>
    public string? EmbedName { get; set; }
    /// <summary>
    /// The json code behind the embed including content components etc
    /// </summary>
    public string JsonCode { get; set; }

    /// <summary>
    /// The user who this embed belongs to
    /// </summary>
    public ulong UserId { get; set; }
}
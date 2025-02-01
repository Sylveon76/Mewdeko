using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

/// <summary>
///     Represents a voice channel role mapping.
/// </summary>
[Table("VcRoles")]
public class VcRole : DbEntity
{
    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    [Required]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the voice channel ID that triggers the role assignment.
    /// </summary>
    [Required]
    public ulong VoiceChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the role ID that will be assigned/unassigned.
    /// </summary>
    [Required]
    public ulong RoleId { get; set; }
}
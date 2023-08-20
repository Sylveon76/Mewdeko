﻿using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

[Table("AFK")]
public class Afk : DbEntity
{
    public ulong UserId { get; set; }
    public ulong GuildId { get; set; }
    public string Message { get; set; }
    public int WasTimed { get; set; } = 0;
    public DateTime? When { get; set; }
}
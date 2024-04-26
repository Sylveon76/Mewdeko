﻿#nullable enable
using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

[Table("MutedUserId")]
public class MutedUserId : DbEntity
{
    public ulong UserId { get; set; }

    // ReSharper disable once InconsistentNaming
    public string? roles { get; set; }

    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    public override int GetHashCode() => UserId.GetHashCode();

    public override bool Equals(object obj) => obj is MutedUserId mui && mui.UserId == UserId;
}
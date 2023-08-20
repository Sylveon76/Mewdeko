﻿#nullable enable
namespace Mewdeko.Database.Models;

public class DiscordUser : DbEntity
{
    public ulong UserId { get; set; }
    public string? Username { get; set; }
    public string? Discriminator { get; set; }
    public string? AvatarId { get; set; }

    public ClubInfo? Club { get; set; }
    public long IsClubAdmin { get; set; }

    public int TotalXp { get; set; }
    public DateTime LastLevelUp { get; set; } = DateTime.UtcNow;
    public DateTime LastXpGain { get; set; } = DateTime.MinValue;
    public XpNotificationLocation NotifyOnLevelUp { get; set; }

    public override int GetHashCode() => UserId.GetHashCode();

    public override string ToString() => $"{Username}#{Discriminator}";

    public long IsDragon { get; set; }
    public string? Pronouns { get; set; }
    public string? PronounsClearedReason { get; set; }

    public long PronounsDisabled { get; set; }

    // public string PndbCache { get; set; }
    public string? Bio { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? ZodiacSign { get; set; }
    public ProfilePrivacyEnum ProfilePrivacy { get; set; } = ProfilePrivacyEnum.Public;
    public uint? ProfileColor { get; set; } = 0;
    public DateTime? Birthday { get; set; }
    public string? SwitchFriendCode { get; set; } = null;
    public BirthdayDisplayModeEnum BirthdayDisplayMode { get; set; } = BirthdayDisplayModeEnum.Default;
    public long StatsOptOut { get; set; } = 0;

    public enum ProfilePrivacyEnum
    {
        Public = 0,
        Private = 1
    }

    public enum BirthdayDisplayModeEnum
    {
        Default,
        MonthOnly,
        YearOnly,
        MonthAndDate,
        Disabled
    }
}
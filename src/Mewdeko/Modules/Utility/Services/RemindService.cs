﻿using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
/// Manages and executes reminders for users at specified times.
/// </summary>
public partial class RemindService : INService
{
    private readonly DiscordSocketClient client;
    private readonly IBotCredentials creds;
    private readonly DbService db;

    private readonly Regex regex =
        MyRegex();

    /// <summary>
    /// Initializes the reminder service, starting the background task to check for and execute reminders.
    /// </summary>
    /// <param name="client">The Discord client used for sending reminder notifications.</param>
    /// <param name="db">The database service for managing reminders.</param>
    /// <param name="creds">The bot's credentials, used for shard management and distribution of tasks.</param>
    public RemindService(DiscordSocketClient client, DbService db, IBotCredentials creds)
    {
        this.client = client;
        this.db = db;
        this.creds = creds;
        _ = StartReminderLoop();
    }

    private async Task StartReminderLoop()
    {
        while (true)
        {
            await Task.Delay(15000).ConfigureAwait(false);
            try
            {
                var now = DateTime.UtcNow;
                var reminders = await GetRemindersBeforeAsync(now).ConfigureAwait(false);
                if (reminders.Count == 0)
                    continue;

                Log.Information($"Executing {reminders.Count} reminders.");

                // make groups of 5, with 1.5 second inbetween each one to ensure against ratelimits
                var i = 0;
                foreach (var group in reminders
                             .GroupBy(_ => ++i / (reminders.Count / 5 + 1)))
                {
                    var executedReminders = group.ToList();
                    await Task.WhenAll(executedReminders.Select(ReminderTimerAction)).ConfigureAwait(false);
                    await RemoveReminders(executedReminders).ConfigureAwait(false);
                    await Task.Delay(1500).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"Error in reminder loop: {ex.Message}");
                Log.Warning(ex.ToString());
            }
        }
    }

    private async Task RemoveReminders(List<Reminder> reminders)
    {
        await using var uow = db.GetDbContext();
        uow.Set<Reminder>()
            .RemoveRange(reminders);

        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    private async Task<List<Reminder>> GetRemindersBeforeAsync(DateTime now)
    {
        await using var uow = db.GetDbContext();
        List<Reminder> reminders;

        if (uow.Database.IsNpgsql())
        {
            reminders = await uow.Reminders
                .Where(x => (int)(x.ServerId / (ulong)Math.Pow(2, 22) % (ulong)creds.TotalShards) == client.ShardId &&
                            x.When < now)
                .ToListAsync();
        }
        else
        {
            reminders = await uow.Reminders
                .FromSqlInterpolated(
                    $"select * from reminders where ((ServerId >> 22) % {creds.TotalShards}) = {client.ShardId} and \"when\" < {now};")
                .ToListAsync();
        }

        return reminders;
    }

    /// <summary>
    /// Parses a remind command input and extracts the reminder details.
    /// </summary>
    /// <param name="input">The input string containing the remind command and its parameters.</param>
    /// <param name="obj">When this method returns, contains the reminder object created from the input.</param>
    /// <returns>true if the input could be parsed; otherwise, false.</returns>
    /// <remarks>
    /// The method uses a regular expression to parse the input and extract reminder details like time and message.
    /// </remarks>
    public bool TryParseRemindMessage(string input, out RemindObject obj)
    {
        var m = regex.Match(input);

        obj = default;
        if (m.Length == 0) return false;

        var values = new Dictionary<string, int>();

        var what = m.Groups["what"].Value;

        if (string.IsNullOrWhiteSpace(what))
        {
            Log.Warning("No message provided for the reminder");
            return false;
        }

        foreach (var groupName in regex.GetGroupNames())
        {
            if (groupName is "0" or "what") continue;
            if (string.IsNullOrWhiteSpace(m.Groups[groupName].Value))
            {
                values[groupName] = 0;
                continue;
            }

            if (!int.TryParse(m.Groups[groupName].Value, out var value))
            {
                Log.Warning($"Reminder regex group {groupName} has invalid value.");
                return false;
            }

            if (value < 1)
            {
                Log.Warning("Reminder time value has to be an integer greater than 0");
                return false;
            }

            values[groupName] = value;
        }

        var ts = new TimeSpan
        (
            30 * values["mo"] + 7 * values["w"] + values["d"],
            values["h"],
            values["m"],
            0
        );

        obj = new RemindObject
        {
            Time = ts, What = what
        };

        return true;
    }

    private async Task ReminderTimerAction(Reminder r)
    {
        try
        {
            IMessageChannel ch;
            if (r.IsPrivate)
            {
                var user = client.GetUser(r.ChannelId);
                if (user == null)
                    return;
                ch = await user.CreateDMChannelAsync().ConfigureAwait(false);
            }
            else
                ch = client.GetGuild(r.ServerId)?.GetTextChannel(r.ChannelId);

            if (ch == null)
                return;

            await ch.EmbedAsync(new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Reminder")
                    .AddField("Created At", r.DateAdded.HasValue ? r.DateAdded.Value.ToLongDateString() : "?")
                    .AddField("By",
                        (await ch.GetUserAsync(r.UserId).ConfigureAwait(false))?.ToString() ?? r.UserId.ToString()),
                r.Message).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Information(ex.Message + $"({r.Id})");
        }
    }

    [GeneratedRegex(
        @"^(?:in\s?)?\s*(?:(?<mo>\d+)(?:\s?(?:months?|mos?),?))?(?:(?:\sand\s|\s*)?(?<w>\d+)(?:\s?(?:weeks?|w),?))?(?:(?:\sand\s|\s*)?(?<d>\d+)(?:\s?(?:days?|d),?))?(?:(?:\sand\s|\s*)?(?<h>\d+)(?:\s?(?:hours?|h),?))?(?:(?:\sand\s|\s*)?(?<m>\d+)(?:\s?(?:minutes?|mins?|m),?))?\s+(?:to:?\s+)?(?<what>(?:\r\n|[\r\n]|.)+)",
        RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex MyRegex();

    /// <summary>
    /// Represents the details of a reminder, including what the reminder is for and the time until the reminder should occur.
    /// </summary>
    public struct RemindObject
    {
        /// <summary>
        /// Gets or sets the message or content of the reminder.
        /// </summary>
        /// <value>
        /// The content of the reminder, describing what the reminder is for.
        /// </value>
        public string? What { get; set; }

        /// <summary>
        /// Gets or sets the duration of time until the reminder should be triggered.
        /// </summary>
        /// <value>
        /// A <see cref="TimeSpan"/> representing the amount of time until the reminder occurs.
        /// </value>
        /// <remarks>
        /// This value is used to calculate the specific datetime when the reminder will be triggered, based on the current time plus the TimeSpan.
        /// </remarks>
        public TimeSpan Time { get; set; }
    }
}
﻿using System.IO;
using System.Text.Json;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Serilog;
using SkiaSharp;
using StackExchange.Redis;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
/// Service for logging user join and leave events.
/// Implements the INService interface.
/// </summary>
public class JoinLeaveLoggerService : INService
{
    private readonly IDataCache cache;
    private readonly IBotCredentials credentials;
    private readonly MewdekoContext dbContext;
    private readonly Timer flushTimer;

    /// <summary>
    /// Constructor for the JoinLeaveLoggerService.
    /// </summary>
    /// <param name="eventHandler">Event handler for user join and leave events.</param>
    /// <param name="cache">Data cache for storing join and leave logs.</param>
    /// <param name="db">Database service for storing join and leave logs.</param>
    /// <param name="credentials">Bot credentials for accessing the Redis database.</param>
    public JoinLeaveLoggerService(EventHandler eventHandler, IDataCache cache, MewdekoContext dbContext,
        IBotCredentials credentials)
    {
        dbContext = dbContext;
        this.credentials = credentials;
        this.cache = cache;

        _ = LoadDataFromSqliteToRedisAsync();
        // Create a timer to flush data from Redis to SQLite every 5 minutes
        var flushInterval = TimeSpan.FromMinutes(5);
        flushTimer = new Timer(async _ => await FlushDataToSqliteAsync(), null, flushInterval, flushInterval);
        eventHandler.UserJoined += LogUserJoined;
        eventHandler.UserLeft += LogUserLeft;
    }

    /// <summary>
    /// Logs when a user joins a guild.
    /// </summary>
    /// <param name="args">The user who joined the guild.</param>
    private async Task LogUserJoined(IGuildUser args)
    {
        var db = cache.Redis.GetDatabase();
        var joinEvent = new JoinLeaveLogs
        {
            GuildId = args.Guild.Id, UserId = args.Id, IsJoin = true
        };

        var serializedEvent = JsonSerializer.Serialize(joinEvent);
        await db.ListRightPushAsync(GetRedisKey(args.Guild.Id), serializedEvent);
    }

    /// <summary>
    /// Logs when a user leaves a guild.
    /// </summary>
    /// <param name="args">The guild the user left.</param>
    /// <param name="arsg2">The user who left the guild.</param>
    private async Task LogUserLeft(IGuild args, IUser arsg2)
    {
        var db = cache.Redis.GetDatabase();
        var leaveEvent = new JoinLeaveLogs
        {
            GuildId = args.Id, UserId = arsg2.Id, IsJoin = false
        };

        var serializedEvent = JsonSerializer.Serialize(leaveEvent);
        await db.ListRightPushAsync(GetRedisKey(args.Id), serializedEvent);
    }

    /// <summary>
    /// Generates a Redis key for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A Redis key for the guild.</returns>
    private string GetRedisKey(ulong guildId)
    {
        return $"{credentials.RedisKey()}:joinLeaveLogs:{guildId}";
    }

    /// <summary>
    /// Calculates the average number of joins per guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>The average number of joins per guild.</returns>
    public double CalculateAverageJoinsPerGuild(ulong guildId)
    {
        var redisDatabase = cache.Redis.GetDatabase();
        var redisKey = GetRedisKey(guildId);
        var allEvents = redisDatabase.ListRangeAsync(redisKey).Result;

        double joinEventsCount = 0;

        foreach (var eventJson in allEvents)
        {
            var eventObj = JsonSerializer.Deserialize<JoinLeaveLogs>(eventJson);
            if (eventObj.IsJoin)
            {
                joinEventsCount++;
            }
        }

        return joinEventsCount / allEvents.Length;
    }

    /// <summary>
    /// Generates a graph of join events for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A stream containing the graph image and an embed for the graph.</returns>
    public async Task<Tuple<Stream, Embed>> GenerateJoinGraphAsync(ulong guildId)
    {
        var redisDatabase = cache.Redis.GetDatabase();
        var redisKey = GetRedisKey(guildId);
        var config = await dbContext.ForGuildId(guildId);

        var joinLogs = await GetJoinLeaveLogsAsync(redisDatabase, redisKey);
        var groupLogs = joinLogs.Where(log => log.IsJoin)
            // ReSharper disable once PossibleInvalidOperationException
            .GroupBy(log => log.DateAdded.Value.Date)
            .Select(group => new
            {
                Date = group.Key, Count = group.Count()
            })
            .OrderBy(x => x.Date)
            .ToList();

        var latestDateInLogs = groupLogs.Any() ? groupLogs.Max(log => log.Date) : DateTime.UtcNow.Date;
        var startDate = latestDateInLogs.AddDays(-10);
        var dateRange = Enumerable.Range(0, 11)
            .Select(i => startDate.AddDays(i));

        var past10DaysData = dateRange
            .GroupJoin(groupLogs, d => d, log => log.Date, (date, logs) => new
            {
                Date = date, Count = logs.Sum(log => log.Count)
            })
            .ToList();

        const int width = 800;
        const int height = 400;
        const int padding = 50;
        const int widthWithPadding = width - 2 * padding;
        const int heightWithPadding = height - 2 * padding;

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(new SKColor(38, 50, 56));

        var gridPaint = new SKPaint
        {
            Color = new SKColor(55, 71, 79), Style = SKPaintStyle.Stroke
        };

        var paint = new SKPaint
        {
            Color = new SKColor(config.JoinGraphColor), StrokeWidth = 3, IsAntialias = true
        };

        var maxCount = past10DaysData.Max(log => (float)log.Count);
        maxCount = (maxCount < 30) ? 30 : ((maxCount / 50) + 1) * 50;
        var scaleX = widthWithPadding / (float)Math.Max(1, (past10DaysData.Count - 1));

        // Draw horizontal grid lines and y-axis labels
        for (var i = 0; i <= maxCount; i += (maxCount <= 30) ? 5 : 50)
        {
            var percentage = i / maxCount;
            var y = height - (padding + percentage * heightWithPadding);

            if (i != 0)
                canvas.DrawLine(padding, y, width - padding, y, gridPaint);

            var label = i.ToString();
            canvas.DrawText(label, padding - 10 - paint.MeasureText(label), y, paint);
        }

        // Draw vertical grid lines and x-axis labels
        SKPath path = null;
        for (var i = 0; i < past10DaysData.Count - 1; i++)
        {
            var countPercentage = past10DaysData[i].Count / maxCount;
            var x1 = padding + i * scaleX;
            var y1 = height - padding - (countPercentage * heightWithPadding);

            // Calculate next point
            var countPercentageNext = past10DaysData[i + 1].Count / maxCount;
            var x2 = padding + (i + 1) * scaleX;
            var y2 = height - padding - (countPercentageNext * heightWithPadding);

            // Calculate control points for a smooth curve
            var cp1 = new SKPoint(x1 + scaleX / 3, y1);
            var cp2 = new SKPoint(x2 - scaleX / 3, y2);

            if (i != 0)
                canvas.DrawLine(x1, padding, x1, height - padding, gridPaint);

            if (path == null)
            {
                path = new SKPath();
                path.MoveTo(x1, y1);
            }
            else
            {
                path.CubicTo(cp1, cp2, new SKPoint(x2, y2));
            }

            var label = past10DaysData[i].Date.ToString("dd/MM");
            canvas.DrawText(label, x1 - (paint.MeasureText(label) / 2), height - (padding / 2), paint);

            // If current index is the penultimate, draw the last label and vertical line
            if (i != past10DaysData.Count - 2)
                continue;
            var lastLabel = past10DaysData[i + 1].Date.ToString("dd/MM");
            canvas.DrawLine(x2, padding, x2, height - padding, gridPaint);
            canvas.DrawText(lastLabel, x2 - (paint.MeasureText(lastLabel) / 2), height - (padding / 2), paint);
        }

        // Draw border lines for grid (bottom line and left line)
        canvas.DrawLine(padding, height - padding, width - padding, height - padding, gridPaint);
        canvas.DrawLine(padding, height - padding, padding, padding, gridPaint);

        paint.Style = SKPaintStyle.Stroke;
        canvas.DrawPath(path, paint);

        var imageStream = new MemoryStream();
        using (var image = SKImage.FromBitmap(bitmap))
        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
        {
            data.SaveTo(imageStream);
        }

        imageStream.Position = 0;
        var embedBuilder = new EmbedBuilder()
            .WithTitle("Join Stats Over the Last 10 Days")
            .WithOkColor() // Assuming JoinGraphColor is a valid color
            .WithCurrentTimestamp();

        // Calculate statistics for the embed
        var totalJoins = past10DaysData.Sum(data => data.Count);
        var peakDay = past10DaysData.OrderByDescending(data => data.Count).First();
        var averageJoins = totalJoins / past10DaysData.Count;

        // Add fields to the embed
        embedBuilder.AddField("Total Joins", totalJoins, true);
        embedBuilder.AddField("Average Joins/Day", $"{averageJoins:N2}", true);
        embedBuilder.AddField("Peak Day", $"{peakDay.Date:dd/MM} ({peakDay.Count} joins)", true);

        // If you have a link for the graph image or it's saved as a file, set the URL
        embedBuilder.WithImageUrl("attachment://joingraph.png");

        // Return the image stream and the built embed
        return new Tuple<Stream, Embed>(imageStream, embedBuilder.Build());
    }

    /// <summary>
    /// Generates a graph of leave events for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A stream containing the graph image and an embed for the graph.</returns>
    public async Task<Tuple<Stream, Embed>> GenerateLeaveGraphAsync(ulong guildId)
    {
        var redisDatabase = cache.Redis.GetDatabase();
        var redisKey = GetRedisKey(guildId);
        var config = await dbContext.ForGuildId(guildId);

        var joinLogs = await GetJoinLeaveLogsAsync(redisDatabase, redisKey);
        var groupLogs = joinLogs.Where(log => !log.IsJoin)
            // ReSharper disable once PossibleInvalidOperationException
            .GroupBy(log => log.DateAdded.Value.Date)
            .Select(group => new
            {
                Date = group.Key, Count = group.Count()
            })
            .OrderBy(x => x.Date)
            .ToList();

        var latestDateInLogs = groupLogs.Any() ? groupLogs.Max(log => log.Date) : DateTime.UtcNow.Date;
        var startDate = latestDateInLogs.AddDays(-10);
        var dateRange = Enumerable.Range(0, 11)
            .Select(i => startDate.AddDays(i));

        var past10DaysData = dateRange
            .GroupJoin(groupLogs, d => d, log => log.Date, (date, logs) => new
            {
                Date = date, Count = logs.Sum(log => log.Count)
            })
            .ToList();

        const int width = 800;
        const int height = 400;
        const int padding = 50;
        var widthWithPadding = width - 2 * padding;
        var heightWithPadding = height - 2 * padding;

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(new SKColor(38, 50, 56));

        var gridPaint = new SKPaint
        {
            Color = new SKColor(55, 71, 79), Style = SKPaintStyle.Stroke
        };

        var paint = new SKPaint
        {
            Color = new SKColor(config.LeaveGraphColor), StrokeWidth = 3, IsAntialias = true
        };

        var maxCount = past10DaysData.Max(log => (float)log.Count);
        maxCount = (maxCount < 30) ? 30 : ((maxCount / 50) + 1) * 50;
        var scaleX = widthWithPadding / (float)Math.Max(1, (past10DaysData.Count - 1));

        // Draw horizontal grid lines and y-axis labels
        for (var i = 0; i <= maxCount; i += (maxCount <= 30) ? 5 : 50)
        {
            var percentage = i / maxCount;
            var y = height - (padding + percentage * heightWithPadding);

            if (i != 0)
                canvas.DrawLine(padding, y, width - padding, y, gridPaint);

            var label = i.ToString();
            canvas.DrawText(label, padding - 10 - paint.MeasureText(label), y, paint);
        }

        // Draw vertical grid lines and x-axis labels
        SKPath path = null;
        for (var i = 0; i < past10DaysData.Count - 1; i++)
        {
            var countPercentage = past10DaysData[i].Count / maxCount;
            var x1 = padding + i * scaleX;
            var y1 = height - padding - (countPercentage * heightWithPadding);

            // Calculate next point
            var countPercentageNext = past10DaysData[i + 1].Count / maxCount;
            var x2 = padding + (i + 1) * scaleX;
            var y2 = height - padding - (countPercentageNext * heightWithPadding);

            // Calculate control points for a smooth curve
            var cp1 = new SKPoint(x1 + scaleX / 3, y1);
            var cp2 = new SKPoint(x2 - scaleX / 3, y2);

            if (i != 0)
                canvas.DrawLine(x1, padding, x1, height - padding, gridPaint);

            if (path == null)
            {
                path = new SKPath();
                path.MoveTo(x1, y1);
            }
            else
            {
                path.CubicTo(cp1, cp2, new SKPoint(x2, y2));
            }

            var label = past10DaysData[i].Date.ToString("dd/MM");
            canvas.DrawText(label, x1 - (paint.MeasureText(label) / 2), height - (padding / 2), paint);

            // If current index is the penultimate, draw the last label and vertical line
            if (i != past10DaysData.Count - 2)
                continue;
            var lastLabel = past10DaysData[i + 1].Date.ToString("dd/MM");
            canvas.DrawLine(x2, padding, x2, height - padding, gridPaint);
            canvas.DrawText(lastLabel, x2 - (paint.MeasureText(lastLabel) / 2), height - (padding / 2), paint);
        }

        // Draw border lines for grid (bottom line and left line)
        canvas.DrawLine(padding, height - padding, width - padding, height - padding, gridPaint);
        canvas.DrawLine(padding, height - padding, padding, padding, gridPaint);

        paint.Style = SKPaintStyle.Stroke;
        canvas.DrawPath(path, paint);

        var imageStream = new MemoryStream();
        using (var image = SKImage.FromBitmap(bitmap))
        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
        {
            data.SaveTo(imageStream);
        }

        imageStream.Position = 0;

        var embedBuilder = new EmbedBuilder()
            .WithTitle("Leave Stats Over the Last 10 Days")
            .WithOkColor()
            .WithCurrentTimestamp();

        var totalLeaves = past10DaysData.Sum(data => data.Count);
        var peakDay = past10DaysData.OrderByDescending(data => data.Count).First();
        var averageLeaves = totalLeaves / past10DaysData.Count;

        embedBuilder.AddField("Total Leaves", totalLeaves, true);
        embedBuilder.AddField("Average Leaves/Day", averageLeaves.ToString("N2"), true);
        embedBuilder.AddField("Peak Day", $"{peakDay.Date:dd/MM} ({peakDay.Count} leaves)", true);
        embedBuilder.WithImageUrl("attachment://leavegraph.png");

        return new Tuple<Stream, Embed>(imageStream, embedBuilder.Build());
    }

    private async Task<List<JoinLeaveLogs>> GetJoinLeaveLogsAsync(IDatabase redisDatabase, string redisKey)
    {
        var allEvents = await redisDatabase.ListRangeAsync(redisKey);

        return allEvents.Select(log => JsonSerializer.Deserialize<JoinLeaveLogs>(log)).ToList();
    }


    private async Task LoadDataFromSqliteToRedisAsync()
    {
        var redisDatabase = cache.Redis.GetDatabase();

        var guildIds = dbContext.JoinLeaveLogs.Select(e => e.GuildId).Distinct().ToList();

        foreach (var guildId in guildIds)
        {
            var joinLeaveLogs = await dbContext.JoinLeaveLogs
                .Where(e => e.GuildId == guildId)
                .ToListAsync();

            var redisKey = GetRedisKey(guildId);
            foreach (var log in joinLeaveLogs)
            {
                await redisDatabase.ListRightPushAsync(redisKey, JsonSerializer.Serialize(log));
            }
        }
    }

    private async Task FlushDataToSqliteAsync()
    {
        Log.Information("Flushing join/leave logs to DB....");

        var redisDatabase = cache.Redis.GetDatabase();
        var guildIds = dbContext.JoinLeaveLogs.Select(e => e.GuildId).Distinct().ToList();

        foreach (var redisKey in guildIds.Select(GetRedisKey))
        {
            while (true)
            {
                var serializedEvent = await redisDatabase.ListLeftPopAsync(redisKey);

                if (serializedEvent.IsNull)
                    break;

                var log = JsonSerializer.Deserialize<JoinLeaveLogs>(serializedEvent.ToString());
                dbContext.JoinLeaveLogs.Add(log);
            }
        }

        await dbContext.SaveChangesAsync();
        Log.Information("Flushing join/leave logs to DB completed");
    }

    /// <summary>
    /// Sets the color for the join graph.
    /// </summary>
    /// <param name="color">The color for the join graph.</param>
    /// <param name="guildId">The ID of the guild.</param>
    public async Task SetJoinColor(uint color, ulong guildId)
    {
        var config = await dbContext.ForGuildId(guildId);
        config.JoinGraphColor = color;
        dbContext.Update(config);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Sets the color for the leave graph.
    /// </summary>
    /// <param name="color">The color for the leave graph.</param>
    /// <param name="guildId">The ID of the guild.</param>
    public async Task SetLeaveColor(uint color, ulong guildId)
    {
        var config = await dbContext.ForGuildId(guildId);
        config.LeaveGraphColor = color;
        dbContext.Update(config);
        await dbContext.SaveChangesAsync();
    }
}
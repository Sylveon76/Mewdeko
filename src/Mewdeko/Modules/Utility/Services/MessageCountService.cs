using System.Data;
using System.Threading;
using Mewdeko.Database.DbContextStuff;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Serilog;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Service for counting messages
/// </summary>
public class MessageCountService : INService
{
    /// <summary>
    ///     Whether the query is for a channel, user, or guild
    /// </summary>
    public enum CountQueryType
    {
        /// <summary>
        ///     Guild
        /// </summary>
        Guild,

        /// <summary>
        ///     Channel
        /// </summary>
        Channel,

        /// <summary>
        ///     User
        /// </summary>
        User
    }

    private readonly DbContextProvider dbContext;
    private readonly ConcurrentDictionary<ulong, int> minCounts = [];
    private readonly HashSet<ulong> countGuilds = [];
    private readonly IMemoryCache cache;
    private readonly Channel<(ulong GuildId, ulong ChannelId, ulong UserId, DateTime Timestamp)> updateChannel;
    private readonly SemaphoreSlim updateLock = new(1);
    private const int CacheMinutes = 30;
    private readonly CancellationTokenSource cts = new();
    private const int BatchSize = 100;

    /// <summary>
    /// </summary>
    public MessageCountService(DbContextProvider dbContext, EventHandler handler, IMemoryCache cache)
    {
        this.dbContext = dbContext;
        this.cache = cache;
        handler.MessageReceived += HandleCount;
        updateChannel = Channel.CreateUnbounded<(ulong, ulong, ulong, DateTime)>();
        _ = ProcessUpdatesAsync(); // Start background processor
    }

    private static readonly AsyncCircuitBreakerPolicy<MessageCount> CircuitBreaker =
        Policy<MessageCount>
            .Handle<Exception>()
            .CircuitBreakerAsync<MessageCount>(
                handledEventsAllowedBeforeBreaking: 10,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (ex, duration) =>
                    Log.Error("Circuit breaker opened for {Duration}s due to: {Error}",
                        duration.TotalSeconds, ex.Exception.Message),
                onReset: () =>
                    Log.Information("Circuit breaker reset"),
                onHalfOpen: () =>
                    Log.Information("Circuit breaker half-open"));

    private static readonly IAsyncPolicy<MessageCount> DatabasePolicy =
        Policy<MessageCount>
            .Handle<PostgresException>(IsTransient)
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    Log.Warning(
                        "Retry {RetryCount} after {Delay}ms for {Key}. Error: {Error}",
                        retryCount,
                        timeSpan.TotalMilliseconds,
                        context["CacheKey"],
                        exception.Exception.Message);
                })
            .WrapAsync(Policy.TimeoutAsync<MessageCount>(TimeSpan.FromSeconds(30)))
            .WrapAsync(Policy.BulkheadAsync<MessageCount>(maxParallelization: 100, maxQueuingActions: 500));

    private async Task ProcessUpdatesAsync()
    {
        var batch = new List<(ulong, ulong, ulong, DateTime)>();

        try
        {
            while (await updateChannel.Reader.WaitToReadAsync(cts.Token))
            {
                while (batch.Count < BatchSize &&
                       updateChannel.Reader.TryRead(out var update))
                {
                    batch.Add(update);
                }

                if (batch.Count <= 0) continue;
                await ProcessBatchAsync(batch);
                batch.Clear();
            }
        }
        catch (OperationCanceledException)
        {}
    }

    private async Task ProcessBatchAsync(List<(ulong GuildId, ulong ChannelId, ulong UserId, DateTime Timestamp)> batch)
    {
        await updateLock.WaitAsync();
        try
        {
            var updates = batch.GroupBy(x => (x.GuildId, x.ChannelId, x.UserId))
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var ((guildId, channelId, userId), count) in updates)
            {
                var key = $"msgcount:{guildId}:{channelId}:{userId}";
                var current = await GetOrCreateMessageCountAsync(guildId, channelId, userId);
                current.Count += (ulong)count;

                cache.Set(key, current, TimeSpan.FromMinutes(CacheMinutes));
            }

            // Batch update timestamps
            await using var db = await dbContext.GetContextAsync();
            var timestamps = batch.Select(x => new MessageTimestamp
            {
                GuildId = x.GuildId, ChannelId = x.ChannelId, UserId = x.UserId, Timestamp = x.Timestamp
            });

            await db.MessageTimestamps.AddRangeAsync(timestamps);
            await db.SaveChangesAsync();
        }
        finally
        {
            updateLock.Release();
        }
    }

    private async Task HandleCount(SocketMessage msg)
    {
        if (!IsValidMessage(msg))
            return;

        var channel = (IGuildChannel)msg.Channel;
        await updateChannel.Writer.WriteAsync((
            channel.GuildId,
            channel.Id,
            msg.Author.Id,
            msg.Timestamp.UtcDateTime
        ));
    }

    /// <summary>
    ///     Private method to retrieve or create a message count record from cache/database.
    /// </summary>
    /// <param name="guildId">The Discord ID of the guild.</param>
    /// <param name="channelId">The Discord ID of the channel.</param>
    /// <param name="userId">The Discord ID of the user.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The existing or newly created MessageCount record.</returns>
    private async Task<MessageCount> GetOrCreateMessageCountAsync(
    ulong guildId,
    ulong channelId,
    ulong userId,
    CancellationToken cancellationToken = default)
{
    var key = $"msgcount:{guildId}:{channelId}:{userId}";

    try
    {
        // Try cache first, outside of retry policy
        if (cache.TryGetValue(key, out MessageCount cachedCount))
        {
            Log.Debug("Cache hit for key {Key}", key);
            return cachedCount;
        }

        Log.Debug("Cache miss for key {Key}", key);

        // Combine policies
        var policy = DatabasePolicy.WrapAsync(CircuitBreaker);

        var count = await policy.ExecuteAsync(async _ =>
        {
            await using var db = await dbContext.GetContextAsync();

            var record = await db.MessageCounts
                .FirstOrDefaultAsync(x =>
                    x.GuildId == guildId &&
                    x.ChannelId == channelId &&
                    x.UserId == userId,
                    cancellationToken);

            if (record == null)
            {
                record = new MessageCount
                {
                    GuildId = guildId,
                    ChannelId = channelId,
                    UserId = userId,
                    Count = 0
                };

                db.MessageCounts.Add(record);
                await db.SaveChangesAsync(cancellationToken);
                Log.Information("Created new message count record for {Key}", key);
            }

            // Cache the result
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(CacheMinutes))
                .RegisterPostEvictionCallback((k, v, r, s) =>
                    Log.Debug("Cache entry {Key} evicted due to {Reason}", k, r));

            cache.Set(key, record, cacheEntryOptions);

            return record;
        }, new Context { ["CacheKey"] = key });

        return count;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        Log.Error(ex, "Failed to get/create message count for {Key}", key);
        throw;
    }
}

    /// <summary>
    ///     Checks if a message is valid for counting based on guild settings and message properties.
    /// </summary>
    /// <param name="message">The Discord message to validate.</param>
    /// <returns>True if the message should be counted, false otherwise.</returns>
    private bool IsValidMessage(SocketMessage message)
    {
        if (countGuilds.Count == 0 ||
            message.Channel is IDMChannel ||
            message.Channel is not IGuildChannel channel ||
            !countGuilds.Contains(channel.GuildId) ||
            message.Author.IsBot)
            return false;

        return !minCounts.TryGetValue(channel.GuildId, out var minValue) ||
               message.Content.Length >= minValue;
    }

    /// <summary>
    ///     Toggles message counting for a guild. If enabled, starts tracking messages.
    ///     If disabled, optionally cleans up existing message data.
    /// </summary>
    /// <param name="guildId">The ID of the guild to toggle message counting for</param>
    /// <returns>True if message counting was enabled, false if it was disabled</returns>
    public async Task<bool> ToggleGuildMessageCount(ulong guildId)
    {
        var wasAdded = false;

        await using var db = await dbContext.GetContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            CancellationToken.None);

        try
        {
            var guildConfig = await db.GuildConfigs
                .Where(x => x.GuildId == guildId)
                .Select(x => new
                {
                    x.UseMessageCount, x.MinMessageLength
                })
                .FirstOrDefaultAsync();

            if (guildConfig == null)
            {
                Log.Warning("Attempted to toggle message count for non-existent guild {GuildId}", guildId);
                return false;
            }

            wasAdded = !guildConfig.UseMessageCount;

            if (wasAdded)
            {
                // Adding the guild to the system
                countGuilds.Add(guildId);
                minCounts[guildId] = guildConfig.MinMessageLength;

                // Load existing counts into cache
                var existingCounts = await db.MessageCounts
                    .Where(x => x.GuildId == guildId)
                    .ToListAsync();

                foreach (var count in existingCounts)
                {
                    var key = $"msgcount:{count.GuildId}:{count.ChannelId}:{count.UserId}";
                    cache.Set(key, count, TimeSpan.FromMinutes(CacheMinutes));
                }
            }
            else
            {
                // Removing the guild from the system
                countGuilds.Remove(guildId);
                minCounts.TryRemove(guildId, out _);

                // Clear cache for this guild
                _ = $"msgcount:{guildId}:*";
                // Note: Implementation of cache pattern removal depends on your cache provider
                // You may need to track keys separately if your cache doesn't support pattern matching
            }

            // Update database
            await db.GuildConfigs
                .Where(x => x.GuildId == guildId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(b => b.UseMessageCount, wasAdded));

            await transaction.CommitAsync();
            return wasAdded;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Log.Error(ex, "Failed to toggle message count for guild {GuildId}", guildId);

            // Revert memory state if transaction failed
            if (wasAdded)
            {
                countGuilds.Remove(guildId);
                minCounts.TryRemove(guildId, out _);
            }
            else
            {
                countGuilds.Add(guildId);
            }

            return !wasAdded;
        }
    }

    /// <summary>
    ///     Gets an array of message counts for the selected entity type along with a boolean indicating if counting is enabled
    /// </summary>
    /// <param name="queryType">The type of query - can be Guild, Channel, or User level</param>
    /// <param name="snowflakeId">The ID of the entity to query</param>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>A tuple containing the array of message counts and whether counting is enabled</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an invalid query type is provided</exception>
    public async Task<(MessageCount[] Counts, bool Enabled)> GetAllCountsForEntity(
        CountQueryType queryType,
        ulong snowflakeId,
        ulong guildId)
    {
        if (!countGuilds.Contains(guildId))
            return (Array.Empty<MessageCount>(), false);

        await using var db = await dbContext.GetContextAsync();

        var query = queryType switch
        {
            CountQueryType.Guild => db.MessageCounts
                .Where(x => x.GuildId == snowflakeId)
                .Include(x => x.Timestamps),

            CountQueryType.Channel => db.MessageCounts
                .Where(x => x.ChannelId == snowflakeId && x.GuildId == guildId)
                .Include(x => x.Timestamps),

            CountQueryType.User => db.MessageCounts
                .Where(x => x.UserId == snowflakeId && x.GuildId == guildId)
                .Include(x => x.Timestamps),

            _ => throw new ArgumentOutOfRangeException(nameof(queryType), queryType, null)
        };

        var counts = await query.ToArrayAsync();

        // Cache the results
        foreach (var count in counts)
        {
            var key = $"msgcount:{count.GuildId}:{count.ChannelId}:{count.UserId}";
            cache.Set(key, count, TimeSpan.FromMinutes(CacheMinutes));
        }

        return (counts, true);
    }

    /// <summary>
    ///     Gets a count for the specified type
    /// </summary>
    /// <param name="queryType">The type of query - can be Guild, Channel, or User level</param>
    /// <param name="guildId">The ID of the guild to query</param>
    /// <param name="snowflakeId">The ID of the entity to query (user/channel ID)</param>
    /// <returns>The total message count for the specified entity</returns>
    /// <exception cref="ArgumentException">Thrown when an invalid query type is provided</exception>
    public async Task<ulong> GetMessageCount(CountQueryType queryType, ulong guildId, ulong snowflakeId)
    {
        if (!countGuilds.Contains(guildId))
            return 0;

        await using var db = await dbContext.GetContextAsync();

        var query = queryType switch
        {
            CountQueryType.Guild => db.MessageCounts
                .Where(x => x.GuildId == guildId),

            CountQueryType.Channel => db.MessageCounts
                .Where(x => x.ChannelId == snowflakeId && x.GuildId == guildId),

            CountQueryType.User => db.MessageCounts
                .Where(x => x.UserId == snowflakeId && x.GuildId == guildId),

            _ => throw new ArgumentException("Invalid query type", nameof(queryType))
        };

        return (ulong)await query.SumAsync(x => (decimal)x.Count);
    }


    /// <summary>
    ///     Gets the busiest hours for a guild
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="days">Number of days to analyze</param>
    /// <returns>Collection of hours and their message counts</returns>
    public async Task<IEnumerable<(int Hour, int Count)>> GetBusiestHours(ulong guildId, int days = 7)
    {
        await using var db = await dbContext.GetContextAsync();
        var startDate = DateTime.UtcNow.AddDays(-Math.Min(days, 30));

        var results = await db.MessageTimestamps
            .Where(t => t.GuildId == guildId && t.Timestamp >= startDate)
            .GroupBy(t => t.Timestamp.Hour)
            .Select(g => new
            {
                Hour = g.Key, Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(24)
            .ToListAsync();

        return results.Select(x => (x.Hour, x.Count));
    }

    /// <summary>
    ///     Gets the busiest days in the guild
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="weeks">Number of weeks to analyze</param>
    /// <returns>Collection of days and their message counts</returns>
    public async Task<IEnumerable<(DayOfWeek Day, int Count)>> GetBusiestDays(ulong guildId, int weeks = 4)
    {
        await using var db = await dbContext.GetContextAsync();
        var startDate = DateTime.UtcNow.AddDays(-Math.Min(7 * weeks, 30));

        var results = await db.MessageTimestamps
            .Where(t => t.GuildId == guildId && t.Timestamp >= startDate)
            .GroupBy(t => t.Timestamp.DayOfWeek)
            .Select(g => new
            {
                Day = g.Key, Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        return results.Select(x => (x.Day, x.Count));
    }

    /// <summary>
    ///     Resets message counts for a specific guild, optionally filtered by user and/or channel.
    ///     Removes both the count records and associated timestamps.
    /// </summary>
    /// <param name="guildId">The ID of the guild to reset counts for</param>
    /// <param name="userId">Optional user ID to reset counts for</param>
    /// <param name="channelId">Optional channel ID to reset counts for</param>
    /// <returns>True if any records were found and removed, false otherwise</returns>
    public async Task<bool> ResetCount(ulong guildId, ulong userId = 0, ulong channelId = 0)
    {
        await using var db = await dbContext.GetContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            CancellationToken.None);

        try
        {
            // Build the base queries
            var countsQuery = db.MessageCounts.Where(x => x.GuildId == guildId);
            var timestampsQuery = db.MessageTimestamps.Where(x => x.GuildId == guildId);

            // Add filters based on parameters
            if (userId != 0)
            {
                countsQuery = countsQuery.Where(x => x.UserId == userId);
                timestampsQuery = timestampsQuery.Where(x => x.UserId == userId);
            }

            if (channelId != 0)
            {
                countsQuery = countsQuery.Where(x => x.ChannelId == channelId);
                timestampsQuery = timestampsQuery.Where(x => x.ChannelId == channelId);
            }

            // Get the counts before deletion for cache cleanup
            var countsToRemove = await countsQuery.ToListAsync();
            if (!countsToRemove.Any())
                return false;

            // Remove records from database
            db.MessageCounts.RemoveRange(countsToRemove);
            await db.SaveChangesAsync();

            // Remove associated timestamps
            await timestampsQuery.ExecuteDeleteAsync();

            // Clear cache entries
            foreach (var key in countsToRemove.Select(count =>
                         $"msgcount:{count.GuildId}:{count.ChannelId}:{count.UserId}"))
            {
                cache.Remove(key);
            }

            await transaction.CommitAsync();
            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Log.Error(ex, "Failed to reset message counts for guild {GuildId}", guildId);
            return false;
        }
    }

    private static bool IsTransient(PostgresException ex)
    {
        return ex.SqlState switch
        {
            "40001" => true, // serialization failure
            "40P01" => true, // deadlock detected
            "08000" => true, // connection_exception
            "08003" => true, // connection_does_not_exist
            "08006" => true, // connection_failure
            "08001" => true, // sqlclient_unable_to_establish_sqlconnection
            "08004" => true, // sqlserver_rejected_establishment_of_sqlconnection
            "57P01" => true, // admin_shutdown
            "57P02" => true, // crash_shutdown
            "57P03" => true, // cannot_connect_now
            "53300" => true, // too_many_connections
            _ => false
        };
    }
}
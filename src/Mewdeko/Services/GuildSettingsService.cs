using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using Mewdeko.Common.Configs;
using Mewdeko.Database.Common;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Services.Impl;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

namespace Mewdeko.Services;

/// <summary>
///     Service responsible for managing Discord guild configurations.
///     Provides methods for retrieving and updating guild settings with proper database context management.
/// </summary>
public class GuildSettingsService
{
    private readonly DbContextProvider dbProvider;
    private readonly BotConfig botSettings;
    private readonly IMemoryCache cache;
    private readonly PerformanceMonitorService perfService;
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> updateLocks;
    private readonly TimeSpan defaultCacheExpiration = TimeSpan.FromMinutes(30);
    private readonly TimeSpan slidingCacheExpiration = TimeSpan.FromMinutes(10);

    // Cache keys
    private static string GetPrefixCacheKey(ulong guildId)
    {
        return $"prefix_{guildId}";
    }

    private static string GetGuildConfigCacheKey(ulong guildId)
    {
        return $"guildconfig_{guildId}";
    }

    private static string GetReactionRolesCacheKey(ulong guildId)
    {
        return $"reactionroles_{guildId}";
    }

    /// <summary>
    ///     Initializes a new instance of the GuildSettingsService.
    /// </summary>
    /// <param name="dbProvider">Provider for database context access.</param>
    /// <param name="botSettings">Service for accessing bot configuration settings.</param>
    /// <param name="memoryCache">Memory cache for storing frequently accessed guild settings.</param>
    /// <param name="perfService">Service for monitoring performance metrics.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required dependency is null.</exception>
    public GuildSettingsService(
        DbContextProvider dbProvider,
        BotConfig botSettings,
        IMemoryCache memoryCache,
        PerformanceMonitorService perfService)
    {
        this.dbProvider = dbProvider ?? throw new ArgumentNullException(nameof(dbProvider));
        this.botSettings = botSettings ?? throw new ArgumentNullException(nameof(botSettings));
        cache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        this.perfService = perfService;
        updateLocks = new ConcurrentDictionary<ulong, SemaphoreSlim>();
    }

    /// <summary>
    ///     Retrieves the command prefix for a specified guild with caching.
    /// </summary>
    /// <param name="guild">The Discord guild to get the prefix for. Can be null for default prefix.</param>
    /// <returns>
    ///     The guild's custom prefix if set, otherwise the default bot prefix.
    ///     Returns default prefix if guild is null.
    /// </returns>
    public async Task<string> GetPrefix(IGuild? guild)
    {
        if (guild == null)
            return botSettings.Prefix;

        var cacheKey = GetPrefixCacheKey(guild.Id);

        // Try to get prefix from cache first
        if (cache.TryGetValue(cacheKey, out string cachedPrefix))
            return cachedPrefix;

        // Cache miss - go to database
        using var perfTracker = perfService.TrackOperation("GetPrefix", "Database");

        await using var db = await dbProvider.GetContextAsync();
        var config = await db.GuildConfigs
            .AsNoTracking()
            .Where(x => x.GuildId == guild.Id)
            .Select(x => x.Prefix)
            .FirstOrDefaultAsync();

        var result = string.IsNullOrWhiteSpace(config)
            ? botSettings.Prefix
            : config;

        // Store in cache with sliding expiration
        cache.Set(cacheKey, result, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = defaultCacheExpiration, SlidingExpiration = slidingCacheExpiration
        });

        return result;
    }

    /// <summary>
    ///     Sets a new command prefix for a specified guild and updates cache.
    /// </summary>
    /// <param name="guild">The Discord guild to set the prefix for.</param>
    /// <param name="prefix">The new prefix to set.</param>
    /// <returns>The newly set prefix.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either guild or prefix is null.</exception>
    public async Task<string> SetPrefix(IGuild guild, string prefix)
    {
        ArgumentNullException.ThrowIfNull(guild);
        ArgumentNullException.ThrowIfNull(prefix);

        // Get lock for this guild to prevent concurrent updates
        var updateLock = updateLocks.GetOrAdd(guild.Id, _ => new SemaphoreSlim(1, 1));

        try
        {
            await updateLock.WaitAsync();

            using var perfTracker = perfService.TrackOperation("SetPrefix", "Database");

            await using var db = await dbProvider.GetContextAsync();
            var config = await db.ForGuildId(guild.Id);

            config.Prefix = prefix;
            await db.SaveChangesAsync();

            // Update cache
            var cacheKey = GetPrefixCacheKey(guild.Id);
            cache.Set(cacheKey, prefix, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = defaultCacheExpiration, SlidingExpiration = slidingCacheExpiration
            });

            // Invalidate full guild config cache if it exists
            cache.Remove(GetGuildConfigCacheKey(guild.Id));

            return prefix;
        }
        finally
        {
            updateLock.Release();
        }
    }

    /// <summary>
    ///     Gets the guild configuration for the specified guild ID with caching.
    /// </summary>
    /// <param name="guildId">The ID of the guild to get configuration for.</param>
    /// <param name="includes">Optional function to include additional related data.</param>
    /// <param name="bypassCache">Set to true to force a database fetch, bypassing cache.</param>
    /// <returns>The guild configuration with any specified includes.</returns>
    /// <exception cref="Exception">Thrown when failing to get guild config.</exception>
    public async Task<GuildConfig> GetGuildConfig(
        ulong guildId,
        Func<DbSet<GuildConfig>, IQueryable<GuildConfig>>? includes = null,
        bool bypassCache = false)
    {
        try
        {
            var cacheKey = GetGuildConfigCacheKey(guildId);

            // Only use cache if no includes are requested and cache bypass is not requested
            if (!bypassCache && includes == null && cache.TryGetValue(cacheKey, out GuildConfig cachedConfig))
            {
                return cachedConfig;
            }

            using var perfTracker = perfService.TrackOperation("GetGuildConfig", "Database");

            await using var db = await dbProvider.GetContextAsync();
            var config = await db.ForGuildId(guildId, includes);

            // Only cache if no includes were requested (simpler caching strategy)
            if (includes == null)
            {
                cache.Set(cacheKey, config, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = defaultCacheExpiration,
                    SlidingExpiration = slidingCacheExpiration
                });
            }

            return config;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get guild config for {GuildId}", guildId);
            throw;
        }
    }

    /// <summary>
    ///     Updates the guild configuration for a specified guild with proper cache invalidation.
    /// </summary>
    /// <param name="guildId">The ID of the guild to update configuration for.</param>
    /// <param name="toUpdate">The updated guild configuration.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="Exception">Thrown when the update operation fails.</exception>
    public async Task UpdateGuildConfig(ulong guildId, GuildConfig toUpdate)
    {
        try
        {
            // Get update lock for this guild
            var updateLock = updateLocks.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));

            try
            {
                await updateLock.WaitAsync();

                using var perfTracker = perfService.TrackOperation("UpdateGuildConfig", "Database");

                await using var db = await dbProvider.GetContextAsync();
                db.GuildConfigs.Update(toUpdate);
                await db.SaveChangesAsync();

                // Update cache with new configuration
                var cacheKey = GetGuildConfigCacheKey(guildId);
                cache.Set(cacheKey, toUpdate, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = defaultCacheExpiration,
                    SlidingExpiration = slidingCacheExpiration
                });

                // Update prefix cache if it exists
                if (!string.IsNullOrWhiteSpace(toUpdate.Prefix))
                {
                    cache.Set(GetPrefixCacheKey(guildId), toUpdate.Prefix, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = defaultCacheExpiration,
                        SlidingExpiration = slidingCacheExpiration
                    });
                }

                // Invalidate any potentially affected sub-caches
                cache.Remove(GetReactionRolesCacheKey(guildId));
            }
            finally
            {
                updateLock.Release();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to update guild config for {GuildId}", guildId);
            throw;
        }
    }

    /// <summary>
    ///     Gets the reaction roles for a specific guild with caching.
    /// </summary>
    /// <param name="guildId">The ID of the guild to get reaction roles for.</param>
    /// <param name="bypassCache">Set to true to force a database fetch, bypassing cache.</param>
    /// <returns>The collection of reaction role messages for the guild.</returns>
    /// <exception cref="Exception">Thrown when failing to get reaction roles.</exception>
    public async Task<IndexedCollection<ReactionRoleMessage>> GetReactionRoles(ulong guildId, bool bypassCache = false)
    {
        try
        {
            var cacheKey = GetReactionRolesCacheKey(guildId);
            if (!bypassCache && cache.TryGetValue(cacheKey, out IndexedCollection<ReactionRoleMessage> cachedRoles))
            {
                return cachedRoles;
            }

            using var perfTracker = perfService.TrackOperation("GetReactionRoles", "Database");

            await using var db = await dbProvider.GetContextAsync();
            var roles = await db.GetReactionRoles(guildId);

            cache.Set(cacheKey, roles, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = defaultCacheExpiration, SlidingExpiration = slidingCacheExpiration
            });

            return roles;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get reaction roles for {GuildId}", guildId);
            throw;
        }
    }
}
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Mewdeko.Common.Configs;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Services.Settings;
using Microsoft.EntityFrameworkCore;
using Serilog;
using ZiggyCreatures.Caching.Fusion;

namespace Mewdeko.Services;

/// <summary>
///     Service responsible for managing and caching Discord guild configurations.
///     Provides optimized access to guild settings with batch processing and caching capabilities.
/// </summary>
public class GuildSettingsService
{
    private readonly DbContextProvider dbProvider;
    private readonly BotConfig botSettings;
    private readonly IFusionCache cache;
    private readonly ConcurrentDictionary<ulong, GuildConfigChanged> changeTracker;
    private readonly Channel<(ulong GuildId, GuildConfig Config)> updateChannel;

    private const string PrefixCacheKey = "prefix:{0}";
    private const string ConfigCacheKey = "guild_config:{0}";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    /// <summary>
    ///     Initializes a new instance of the GuildSettingsService.
    /// </summary>
    /// <param name="dbProvider">Provider for database context access.</param>
    /// <param name="botSettings">Service for accessing bot configuration settings.</param>
    /// <param name="cache">Fusion cache instance for storing guild configurations.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required dependency is null.</exception>
    public GuildSettingsService(
        DbContextProvider dbProvider,
        BotConfig botSettings,
        IFusionCache cache)
    {
        this.dbProvider = dbProvider;
        this.botSettings = botSettings;
        this.cache = cache;
        changeTracker = new ConcurrentDictionary<ulong, GuildConfigChanged>();
        updateChannel = Channel.CreateUnbounded<(ulong, GuildConfig)>(
            new UnboundedChannelOptions { SingleReader = true });

        // Start background save processor
        _ = ProcessConfigUpdatesAsync();
    }

    /// <summary>
    ///     Retrieves the command prefix for a specified guild.
    /// </summary>
    /// <param name="guild">The Discord guild to get the prefix for. Can be null for default prefix.</param>
    /// <returns>
    ///     The guild's custom prefix if set, otherwise the default bot prefix.
    ///     Returns default prefix if guild is null.
    /// </returns>
    public async Task<string?> GetPrefix(IGuild? guild)
    {
        if (guild == null)
            return botSettings.Prefix;

        var cacheKey = string.Format(PrefixCacheKey, guild.Id);
        return await cache.GetOrSetAsync(cacheKey,
            async _ => {
                var config = await GetGuildConfigFromCache(guild.Id);
                return string.IsNullOrWhiteSpace(config.Prefix)
                    ? botSettings.Prefix
                    : config.Prefix;
            },
            new FusionCacheEntryOptions { Duration = CacheDuration });
    }


    /// <summary>
    ///     Sets a new command prefix for a specified guild.
    /// </summary>
    /// <param name="guild">The Discord guild to set the prefix for.</param>
    /// <param name="prefix">The new prefix to set.</param>
    /// <returns>The newly set prefix.</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when either guild or prefix is null.
    /// </exception>
    public async Task<string> SetPrefix(IGuild guild, string prefix)
    {
        ArgumentNullException.ThrowIfNull(guild);
        ArgumentNullException.ThrowIfNull(prefix);

        var config = await GetGuildConfigFromCache(guild.Id);
        config.Prefix = prefix;

        // Mark config as changed
        changeTracker.AddOrUpdate(guild.Id,
            _ => new GuildConfigChanged { LastModified = DateTime.UtcNow },
            (_, existing) => {
                existing.LastModified = DateTime.UtcNow;
                return existing;
            });

        // Queue update
        await updateChannel.Writer.WriteAsync((guild.Id, config));

        // Invalidate prefix cache
        await cache.RemoveAsync(string.Format(PrefixCacheKey, guild.Id));

        return prefix;
    }

    /// <summary>
    ///     Gets the guild configuration for the specified guild ID.
    /// </summary>
    /// <param name="guildId">The ID of the guild to get configuration for.</param>
    /// <param name="includes">Optional function to include additional related data.</param>
    /// <param name="callerName">The name of the calling method.</param>
    /// <param name="filePath">The file path of the calling method.</param>
    /// <returns>The guild configuration.</returns>
    /// <exception cref="Exception">Thrown when failing to get guild config.</exception>
    public async Task<GuildConfig> GetGuildConfig(ulong guildId,
        Func<DbSet<GuildConfig>, IQueryable<GuildConfig>>? includes = null,
        [CallerMemberName] string callerName = "",
        [CallerFilePath] string filePath = "")
    {
        var cacheKey = string.Format(ConfigCacheKey, guildId);
        try
        {
            return await cache.GetOrSetAsync(cacheKey,
                async _ =>
                {
                    await using var dbContext = await dbProvider.GetContextAsync();
                    var sw = new Stopwatch();
                    sw.Start();
                    var toLoad = await dbContext.ForGuildId(guildId, includes);
                    return toLoad;
                },
                new FusionCacheEntryOptions { Duration = CacheDuration });
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to get guild config");
            throw;
        }
    }

    /// <summary>
    ///     Updates the guild configuration.
    /// </summary>
    /// <param name="guildId">The ID of the guild to update configuration for.</param>
    /// <param name="toUpdate">The updated guild configuration.</param>
    /// <param name="callerName">The name of the calling method.</param>
    /// <param name="filePath">The file path of the calling method.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task UpdateGuildConfig(ulong guildId, GuildConfig toUpdate,
        [CallerMemberName] string callerName = "",
        [CallerFilePath] string filePath = "")
    {
        try
        {
            // Mark config as changed
            changeTracker.AddOrUpdate(guildId,
                _ => new GuildConfigChanged { LastModified = DateTime.UtcNow },
                (_, existing) => {
                    existing.LastModified = DateTime.UtcNow;
                    return existing;
                });

            // Queue update for batch processing
            await updateChannel.Writer.WriteAsync((guildId, toUpdate));

            // Update cache immediately
            var cacheKey = string.Format(ConfigCacheKey, guildId);
            await cache.SetAsync(cacheKey, toUpdate, CacheDuration);
        }
        catch (Exception e)
        {
            Log.Error("Executed from {CallerName} in {FilePath}", callerName, filePath);
            Log.Error(e, "There was an issue queuing a GuildConfig update");
            throw;
        }
    }

    private async Task<GuildConfig> GetGuildConfigFromCache(ulong guildId)
    {
        var cacheKey = string.Format(ConfigCacheKey, guildId);
        return await cache.GetOrSetAsync(cacheKey,
            async _ => {
                await using var db = await dbProvider.GetContextAsync();
                return await db.ForGuildId(guildId);
            },
            new FusionCacheEntryOptions { Duration = CacheDuration });
    }

    private async Task ProcessConfigUpdatesAsync()
    {
        var batch = new List<(ulong GuildId, GuildConfig Config)>();

        while (await updateChannel.Reader.WaitToReadAsync())
        {
            while (batch.Count < 100 &&
                   updateChannel.Reader.TryRead(out var update))
            {
                batch.Add(update);
            }

            if (batch.Count <= 0) continue;
            await SaveConfigBatchAsync(batch);
            batch.Clear();
        }
    }

    private async Task SaveConfigBatchAsync(
        IReadOnlyCollection<(ulong GuildId, GuildConfig Config)> updates)
    {
        try
        {
            await using var db = await dbProvider.GetContextAsync();

            foreach (var (_, config) in updates)
            {
                db.GuildConfigs.Update(config);
            }

            await db.SaveChangesAsync();

            // Update cache for saved configs
            foreach (var (guildId, config) in updates)
            {
                var cacheKey = string.Format(ConfigCacheKey, guildId);
                await cache.SetAsync(cacheKey, config, CacheDuration);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error saving guild configs batch");
        }
    }

    private class GuildConfigChanged
    {
        public DateTime LastModified { get; set; }
    }
}
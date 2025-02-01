using Mewdeko.Common.Configs;
using Mewdeko.Database.Common;
using Mewdeko.Database.DbContextStuff;
using Microsoft.EntityFrameworkCore;
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

    /// <summary>
    ///     Initializes a new instance of the GuildSettingsService.
    /// </summary>
    /// <param name="dbProvider">Provider for database context access.</param>
    /// <param name="botSettings">Service for accessing bot configuration settings.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required dependency is null.</exception>
    public GuildSettingsService(
        DbContextProvider dbProvider,
        BotConfig botSettings)
    {
        this.dbProvider = dbProvider ?? throw new ArgumentNullException(nameof(dbProvider));
        this.botSettings = botSettings ?? throw new ArgumentNullException(nameof(botSettings));
    }

    /// <summary>
    ///     Retrieves the command prefix for a specified guild.
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

        await using var db = await dbProvider.GetContextAsync();
        var config = await db.GuildConfigs
            .AsNoTracking()
            .Where(x => x.GuildId == guild.Id)
            .Select(x => x.Prefix)
            .FirstOrDefaultAsync();

        return string.IsNullOrWhiteSpace(config)
            ? botSettings.Prefix
            : config;
    }

    /// <summary>
    ///     Sets a new command prefix for a specified guild.
    /// </summary>
    /// <param name="guild">The Discord guild to set the prefix for.</param>
    /// <param name="prefix">The new prefix to set.</param>
    /// <returns>The newly set prefix.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either guild or prefix is null.</exception>
    public async Task<string> SetPrefix(IGuild guild, string prefix)
    {
        ArgumentNullException.ThrowIfNull(guild);
        ArgumentNullException.ThrowIfNull(prefix);

        await using var db = await dbProvider.GetContextAsync();
        var config = await db.ForGuildId(guild.Id);

        config.Prefix = prefix;
        await db.SaveChangesAsync();

        return prefix;
    }

    /// <summary>
    ///     Gets the guild configuration for the specified guild ID, optionally including related entities.
    /// </summary>
    /// <param name="guildId">The ID of the guild to get configuration for.</param>
    /// <param name="includes">Optional function to include additional related data.</param>
    /// <returns>The guild configuration with any specified includes.</returns>
    /// <remarks>
    ///     This method leverages the ForGuildId extension method which handles creation of default configurations
    ///     if none exist. It also ensures proper initialization of warnings and other default settings.
    /// </remarks>
    /// <exception cref="Exception">Thrown when failing to get guild config.</exception>
    public async Task<GuildConfig> GetGuildConfig(ulong guildId,
        Func<DbSet<GuildConfig>, IQueryable<GuildConfig>>? includes = null)
    {
        try
        {
            await using var db = await dbProvider.GetContextAsync();
            return await db.ForGuildId(guildId, includes);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get guild config for {GuildId}", guildId);
            throw;
        }
    }

    /// <summary>
    ///     Updates the guild configuration for a specified guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to update configuration for.</param>
    /// <param name="toUpdate">The updated guild configuration.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="Exception">Thrown when the update operation fails.</exception>
    public async Task UpdateGuildConfig(ulong guildId, GuildConfig toUpdate)
    {
        try
        {
            await using var db = await dbProvider.GetContextAsync();
            db.GuildConfigs.Update(toUpdate);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to update guild config for {GuildId}", guildId);
            throw;
        }
    }

    /// <summary>
    ///     Gets the reaction roles for a specific guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to get reaction roles for.</param>
    /// <returns>The collection of reaction role messages for the guild.</returns>
    /// <exception cref="Exception">Thrown when failing to get reaction roles.</exception>
    public async Task<IndexedCollection<ReactionRoleMessage>> GetReactionRoles(ulong guildId)
    {
        try
        {
            await using var db = await dbProvider.GetContextAsync();
            return await db.GetReactionRoles(guildId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get reaction roles for {GuildId}", guildId);
            throw;
        }
    }
}
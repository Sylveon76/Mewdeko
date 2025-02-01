using Mewdeko.Database.Common;
using Mewdeko.Database.DbContextStuff;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
/// Service for managing reaction-based role assignments and removals.
/// </summary>
public class RoleCommandsService : INService
{
    private readonly DbContextProvider dbProvider;
    private readonly GuildSettingsService guildSettings;

    /// <summary>
    /// Initializes a new instance of the RoleCommandsService.
    /// </summary>
    /// <param name="dbProvider">Provider for database context access.</param>
    /// <param name="eventHandler">Event handler for Discord events.</param>
    /// <param name="bot">The main bot instance.</param>
    /// <param name="guildSettings">Service for accessing guild configurations.</param>
    public RoleCommandsService(
        DbContextProvider dbProvider,
        EventHandler eventHandler,
        Mewdeko bot,
        GuildSettingsService guildSettings)
    {
        this.dbProvider = dbProvider;
        this.guildSettings = guildSettings;

        eventHandler.ReactionAdded += HandleReactionAdded;
        eventHandler.ReactionRemoved += HandleReactionRemoved;
    }

    /// <summary>
    /// Handles when a reaction is added to a message.
    /// </summary>
    private async Task HandleReactionAdded(
        Cacheable<IUserMessage, ulong> msg,
        Cacheable<IMessageChannel, ulong> chan,
        SocketReaction reaction)
    {
        try
        {
            if (!reaction.User.IsSpecified ||
                reaction.User.Value.IsBot ||
                reaction.User.Value is not SocketGuildUser gusr)
            {
                return;
            }

            if (chan.Value is not SocketGuildChannel gch)
                return;

            var reactRoles = await guildSettings.GetReactionRoles(gch.Guild.Id);
            if (reactRoles == null || reactRoles.Count == 0)
                return;

            var message = msg.HasValue ? msg.Value : await msg.GetOrDownloadAsync();
            var conf = reactRoles.FirstOrDefault(x => x.MessageId == message.Id);

            // compare emote names for backwards compatibility
            var reactionRole = conf?.ReactionRoles.Find(x =>
                x.EmoteName == reaction.Emote.Name || x.EmoteName == reaction.Emote.ToString());
            if (reactionRole == null)
                return;

            if (conf.Exclusive)
            {
                await HandleExclusiveRole(gusr, msg, conf, reactionRole, reaction);
            }

            var toAdd = gusr.Guild.GetRole(reactionRole.RoleId);
            if (toAdd != null && !gusr.Roles.Contains(toAdd))
                await gusr.AddRolesAsync(new[] { toAdd });
        }
        catch (Exception ex)
        {
            var gch = chan.Value as IGuildChannel;
            Log.Error(ex, "Reaction Role Add failed in {Guild}", gch?.Guild);
        }
    }

    private static async Task HandleExclusiveRole(
        SocketGuildUser user,
        Cacheable<IUserMessage, ulong> msg,
        ReactionRoleMessage conf,
        ReactionRole currentRole,
        SocketReaction reaction)
    {
        var roleIds = conf.ReactionRoles
            .Select(x => x.RoleId)
            .Where(x => x != currentRole.RoleId)
            .Select(x => user.Guild.GetRole(x))
            .Where(x => x != null);

        try
        {
            // Remove all other reactions user added to the message
            var message = await msg.GetOrDownloadAsync();
            foreach (var (key, _) in message.Reactions)
            {
                if (key.Name == reaction.Emote.Name)
                    continue;

                try
                {
                    await message.RemoveReactionAsync(key, user);
                }
                catch
                {
                    // ignored
                }

                await Task.Delay(100);
            }
        }
        catch
        {
            // ignored
        }

        await user.RemoveRolesAsync(roleIds);
    }

    /// <summary>
    /// Handles when a reaction is removed from a message.
    /// </summary>
    private async Task HandleReactionRemoved(
        Cacheable<IUserMessage, ulong> msg,
        Cacheable<IMessageChannel, ulong> chan,
        SocketReaction reaction)
    {
        try
        {
            if (!reaction.User.IsSpecified ||
                reaction.User.Value.IsBot ||
                reaction.User.Value is not SocketGuildUser gusr)
            {
                return;
            }

            if (chan.Value is not SocketGuildChannel gch)
                return;

            var reactRoles = await guildSettings.GetReactionRoles(gch.Guild.Id);
            if (reactRoles == null || reactRoles.Count == 0)
                return;

            var message = msg.HasValue ? msg.Value : await msg.GetOrDownloadAsync();
            var conf = reactRoles.FirstOrDefault(x => x.MessageId == message.Id);

            var reactionRole = conf?.ReactionRoles.Find(x =>
                x.EmoteName == reaction.Emote.Name || x.EmoteName == reaction.Emote.ToString());
            if (reactionRole == null)
                return;

            var toRemove = gusr.Guild.GetRole(reactionRole.RoleId);
            if (toRemove != null && gusr.Roles.Contains(toRemove))
                await gusr.RemoveRolesAsync(new[] { toRemove });
        }
        catch (Exception ex)
        {
            var gch = chan.Value as IGuildChannel;
            Log.Error(ex, "Reaction Role Remove failed in {Guild}", gch?.Guild);
        }
    }

    /// <summary>
    /// Gets all reaction role messages for a guild.
    /// </summary>
    /// <param name="guildId">ID of the guild.</param>
    /// <returns>A tuple containing success status and collection of reaction role messages.</returns>
    public async Task<(bool Success, IndexedCollection<ReactionRoleMessage> Messages)> Get(ulong guildId)
    {
        var reactRoles = await guildSettings.GetReactionRoles(guildId);
        return reactRoles == null || reactRoles.Count == 0
            ? (false, null)
            : (true, reactRoles);
    }

    /// <summary>
    /// Adds a new reaction role message to a guild.
    /// </summary>
    /// <param name="guildId">ID of the guild.</param>
    /// <param name="reactionRoleMessage">The reaction role message to add.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public async Task<bool> Add(ulong guildId, ReactionRoleMessage reactionRoleMessage)
    {
        var config = await guildSettings.GetGuildConfig(guildId, set => set
            .Include(x => x.ReactionRoleMessages)
            .ThenInclude(x => x.ReactionRoles));

        config.ReactionRoleMessages.Add(reactionRoleMessage);
        await guildSettings.UpdateGuildConfig(guildId, config);
        return true;
    }

    /// <summary>
    /// Removes a reaction role message and its associated reaction roles from a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="index">The index of the reaction role message to remove.</param>
    public async Task Remove(ulong guildId, int index)
    {
        await using var db = await dbProvider.GetContextAsync();
        var config = await db.GuildConfigs
            .Include(x => x.ReactionRoleMessages)
            .ThenInclude(x => x.ReactionRoles)
            .FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (config?.ReactionRoleMessages == null || index >= config.ReactionRoleMessages.Count)
            return;
        var messageToRemove = config.ReactionRoleMessages[index];
        db.Set<ReactionRoleMessage>().Remove(messageToRemove);

        await db.SaveChangesAsync();
    }
}
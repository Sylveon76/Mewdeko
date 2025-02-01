using System.Collections.Concurrent;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.DbContextStuff;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     The voice channel role service. Pain.
/// </summary>
public class VcRoleService : INService, IReadyExecutor
{
    private readonly DiscordShardedClient client;
    private readonly DbContextProvider dbProvider;
    private readonly GuildSettingsService guildSettingsService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="VcRoleService" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="bot">The bot instance.</param>
    /// <param name="db">The database service.</param>
    /// <param name="eventHandler">The event handler.</param>
    /// <param name="guildSettingsService">The guild settings service.</param>
    public VcRoleService(DiscordShardedClient client, Mewdeko bot, DbContextProvider dbProvider,
        EventHandler eventHandler,
        GuildSettingsService guildSettingsService)
    {
        // Assigning the database service and the Discord client
        this.dbProvider = dbProvider;
        this.guildSettingsService = guildSettingsService;
        this.client = client;

        // Subscribing to the UserVoiceStateUpdated event
        eventHandler.UserVoiceStateUpdated += ClientOnUserVoiceStateUpdated;

        ToAssign = new NonBlocking.ConcurrentDictionary<ulong, ConcurrentQueue<(bool, IGuildUser, IRole)>>();

        // Getting all guild configurations and initializing VC roles for each guild

        // Starting a new task that continuously assigns or removes roles from users
        Task.Run(async () =>
        {
            while (true)
            {
                var tasks = ToAssign.Values.Select(queue => Task.Run(async () =>
                {
                    while (queue.TryDequeue(out var item))
                    {
                        var (add, user, role) = item;
                        if (add)
                        {
                            if (!user.RoleIds.Contains(role.Id))
                            {
                                try
                                {
                                    await user.AddRoleAsync(role).ConfigureAwait(false);
                                }
                                catch
                                {
                                    // ignored
                                }
                            }
                        }
                        else
                        {
                            if (user.RoleIds.Contains(role.Id))
                            {
                                try
                                {
                                    await user.RemoveRoleAsync(role).ConfigureAwait(false);
                                }
                                catch
                                {
                                    // ignored
                                }
                            }
                        }

                        await Task.Delay(250).ConfigureAwait(false);
                    }
                }));

                await Task.WhenAll(tasks.Append(Task.Delay(1000))).ConfigureAwait(false);
            }
        });

        // Subscribing to the LeftGuild and JoinedGuild events
        eventHandler.LeftGuild += _client_LeftGuild;
        eventHandler.JoinedGuild += Bot_JoinedGuild;
    }

    /// <summary>
    ///     A dictionary that maps guild IDs to another dictionary, which maps voice channel IDs to roles.
    /// </summary>
    public NonBlocking.ConcurrentDictionary<ulong, NonBlocking.ConcurrentDictionary<ulong, IRole>> VcRoles { get; } =
        new();

    /// <summary>
    ///     A dictionary that maps guild IDs to a queue of tuples, each containing a boolean indicating whether to add or
    ///     remove a role, a guild user, and a role.
    /// </summary>
    private NonBlocking.ConcurrentDictionary<ulong, ConcurrentQueue<(bool, IGuildUser, IRole)>> ToAssign { get; }

    /// <summary>
    ///     Event handler for when the bot joins a guild. Initializes voice channel roles for the guild.
    /// </summary>
    /// <param name="arg">The guild configuration.</param>
    private async Task Bot_JoinedGuild(IGuild guild)
    {
        await using var db = await dbProvider.GetContextAsync();
        var conf = await db.VcRoles.Where(x => x.GuildId == guild.Id).ToArrayAsync();
        if (conf.Length == 0)
            return;
        await InitializeVcRole(conf);
    }

    /// <summary>
    ///     Event handler for when the bot leaves a guild. Removes voice channel roles for the guild.
    /// </summary>
    /// <param name="arg">The guild.</param>
    private Task _client_LeftGuild(SocketGuild arg)
    {
        VcRoles.TryRemove(arg.Id, out _);
        ToAssign.TryRemove(arg.Id, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Initializes voice channel roles for a guild.
    /// </summary>
    /// <param name="gconf">The guild configuration.</param>
    private async Task InitializeVcRole(VcRole[] config)
    {
        await Task.Yield();
        var g = client.Guilds.FirstOrDefault(x => x.Id == config.FirstOrDefault().GuildId);
        if (g == null )
            return;

        var infos = new NonBlocking.ConcurrentDictionary<ulong, IRole>();
        var missingRoles = new List<VcRole>();
        VcRoles.AddOrUpdate(config.FirstOrDefault().GuildId, infos, delegate { return infos; });
        foreach (var ri in config)
        {
            var role = g.Roles.FirstOrDefault(x => x.Id == ri.RoleId);
            if (role == null)
            {
                missingRoles.Add(ri);
                continue;
            }

            infos.TryAdd(ri.VoiceChannelId, role);
        }

        if (missingRoles.Count > 0)
        {
            await using var dbContext = await dbProvider.GetContextAsync();
            Log.Warning("Removing {MissingRolesCount} missing roles from {VcRoleServiceName}", missingRoles.Count,
                nameof(VcRoleService));
            dbContext.RemoveRange(missingRoles);
            await dbContext.SaveChangesAsync();
        }
    }

    /// <summary>
    ///     Adds a voice channel role to a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to add the role to.</param>
    /// <param name="role">The role to add.</param>
    /// <param name="vcId">The ID of the voice channel to associate the role with.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task AddVcRole(ulong guildId, IRole role, ulong vcId)
    {
        ArgumentNullException.ThrowIfNull(role);

        var guildVcRoles = VcRoles.GetOrAdd(guildId, new NonBlocking.ConcurrentDictionary<ulong, IRole>());
        guildVcRoles.AddOrUpdate(vcId, role, (_, _) => role);

        const int maxRetries = 3;
        var attempt = 0;

        while (attempt < maxRetries)
        {
            try
            {
                await using var dbContext = await dbProvider.GetContextAsync();

                var toDelete = await dbContext.VcRoles.FirstOrDefaultAsync(x => x.VoiceChannelId == vcId);
                if (toDelete != null) dbContext.Remove(toDelete);

                dbContext.VcRoles.Add(new VcRole
                {
                    GuildId = guildId,
                    VoiceChannelId = vcId,
                    RoleId = role.Id
                });
                await dbContext.SaveChangesAsync();
                return;
            }
            catch (DbUpdateConcurrencyException)
            {
                attempt++;
                if (attempt >= maxRetries)
                {
                    Log.Error("Failed to save VcRole after {Attempts} attempts due to concurrency", attempt);
                    throw;
                }
                await Task.Delay(100 * attempt);
            }
        }
    }

    /// <summary>
    ///     Removes a voice channel role from a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to remove the role from.</param>
    /// <param name="vcId">The ID of the voice channel to disassociate the role from.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains a boolean indicating whether the operation was
    ///     successful.
    /// </returns>
    public async Task<bool> RemoveVcRole(ulong guildId, ulong vcId)
    {
        if (!VcRoles.TryGetValue(guildId, out var guildVcRoles))
            return false;

        if (!guildVcRoles.TryRemove(vcId, out _))
            return false;

        await using var dbContext = await dbProvider.GetContextAsync();
        var toRemove = await dbContext.VcRoles.FirstOrDefaultAsync(x => x.VoiceChannelId == vcId);
        if (toRemove is null)
            return false;
        dbContext.VcRoles.Remove(toRemove);
        await dbContext.SaveChangesAsync();

        return true;
    }

    /// <summary>
    ///     Event handler for when a user's voice state is updated. Assigns or removes roles based on the user's new voice
    ///     state.
    /// </summary>
    /// <param name="usr">The user whose voice state was updated.</param>
    /// <param name="oldState">The user's old voice state.</param>
    /// <param name="newState">The user's new voice state.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private Task ClientOnUserVoiceStateUpdated(SocketUser usr, SocketVoiceState oldState,
        SocketVoiceState newState)
    {
        if (usr is not SocketGuildUser gusr)
            return Task.CompletedTask;

        var oldVc = oldState.VoiceChannel;
        var newVc = newState.VoiceChannel;
        _ = Task.Run(() =>
        {
            try
            {
                if (oldVc == newVc) return;
                var guildId = newVc?.Guild.Id ?? oldVc.Guild.Id;

                if (!VcRoles.TryGetValue(guildId, out var guildVcRoles)) return;
                //remove old
                if (oldVc != null && guildVcRoles.TryGetValue(oldVc.Id, out var role))
                    Assign(false, gusr, role);
                //add new
                if (newVc != null && guildVcRoles.TryGetValue(newVc.Id, out role)) Assign(true, gusr, role);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error in VcRoleService VoiceStateUpdate");
            }
        });
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Assigns a role to a user in a guild.
    /// </summary>
    /// <param name="v">A boolean indicating whether to add or remove the role.</param>
    /// <param name="gusr">The user in the guild.</param>
    /// <param name="role">The role to assign or remove.</param>
    private void Assign(bool v, SocketGuildUser gusr, IRole role)
    {
        // Get or create a queue for the guild
        var queue = ToAssign.GetOrAdd(gusr.Guild.Id, new ConcurrentQueue<(bool, IGuildUser, IRole)>());

        // Enqueue the operation (add or remove role)
        queue.Enqueue((v, gusr, role));
    }

    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        var guilds = client.Guilds;
        foreach (var guild in guilds)
        {
            try
            {
                await using var db = await dbProvider.GetContextAsync();
                var conf = await db.VcRoles.Where(x => x.GuildId == guild.Id).ToArrayAsync();
                Log.Information($"{guild} has {conf.Length} VCRs");
                if (conf.Length==0)
                    continue;
                await InitializeVcRole(conf);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading VC roles for guild {GuildId}", guild.Id);
            }
        }
    }
}
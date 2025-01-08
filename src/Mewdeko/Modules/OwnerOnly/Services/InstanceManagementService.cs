using System.Net.Http;
using System.Text.Json;
using System.Threading;
using LinqToDB;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Controllers;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Services.Impl;
using Serilog;

namespace Mewdeko.Modules.OwnerOnly.Services;

/// <summary>
/// Service responsible for managing and monitoring bot instances running on the local machine.
/// </summary>
public class InstanceManagementService : INService, IReadyExecutor
{
    private readonly DbContextProvider provider;
    private readonly IHttpClientFactory factory;
    private readonly DiscordShardedClient client;
    private readonly string apiKey;

    /// <summary>
    /// Initializes a new instance of the BotInstanceService.
    /// </summary>
    /// <param name="provider">The database context provider.</param>
    /// <param name="factory">The HTTP client factory.</param>
    public InstanceManagementService(
        DbContextProvider provider,
        IHttpClientFactory factory, DiscordShardedClient client)
    {
        var creds = new BotCredentials();
        apiKey = creds.ApiKey;
        this.provider = provider;
        this.factory = factory;
        this.client = client;
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var httpClient = factory.CreateClient();
        httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return httpClient;
    }

    /// <summary>
    /// Called when bot is ready. Registers itself if master instance.
    /// </summary>
    public async Task OnReadyAsync()
    {
        var creds = new BotCredentials();

        // If master instance, make sure we're registered
        if (creds.IsMasterInstance)
        {
            try
            {
                await using var db = await provider.GetContextAsync();
                var exists = await db.BotInstances.AnyAsync(x => x.Port == creds.ApiPort);

                if (!exists)
                {
                    Log.Information("Registering self as master instance on port {Port}", creds.ApiPort);

                    db.BotInstances.Add(new BotInstance
                    {
                        Port = creds.ApiPort,
                        BotId = client.CurrentUser.Id,
                        BotName = client.CurrentUser.Username,
                        BotAvatar = client.CurrentUser.GetAvatarUrl(),
                        IsActive = true,
                        LastStatusUpdate = DateTime.UtcNow
                    });

                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register self as instance");
            }

            var periodic = new PeriodicTimer(TimeSpan.FromMinutes(1));
            do
            {
                await MonitorInstancesAsync();
            } while (await periodic.WaitForNextTickAsync());
        }
        else
        {
            Log.Information("Not registering self as instance. Not marked as master instance");
        }

    }

    /// <summary>
    /// Registers a new bot instance with the specified port number.
    /// </summary>
    /// <param name="port">The TCP port number the bot instance is listening on.</param>
    /// <returns>
    /// A tuple containing:
    /// - Success: Whether the registration was successful
    /// - Status: The bot's status information if successful, null otherwise
    /// - Reason: The reason for failure if not successful, null otherwise
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when port number is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when not running on master instance.</exception>
    public async Task<(bool Success, BotStatus.BotStatusModel? Status, string? Reason)> AddInstanceAsync(int port)
    {
        if (!new BotCredentials().IsMasterInstance)
            throw new InvalidOperationException("Can only add instances from master bot.");

        if (port is < 1024 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1024 and 65535");

        await using var db = await provider.GetContextAsync();
        if (await db.BotInstances.AnyAsync(x => x.Port == port))
            return (false, null, "instance_already_exists");

        var status = await GetInstanceStatusAsync(port);
        if (status == null)
            return (false, null, "instance_not_responding");

        db.BotInstances.Add(new BotInstance
        {
            Port = port,
            BotId = status.BotId,
            BotName = status.BotName,
            BotAvatar = status.BotAvatar,
            IsActive = true,
            LastStatusUpdate = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        return (true, status, null);
    }

    /// <summary>
    /// Retrieves the current status of a bot instance.
    /// </summary>
    /// <param name="port">The port number of the bot instance.</param>
    /// <returns>The bot's status information if available, null if the instance is unreachable.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when port number is invalid.</exception>
    public async Task<BotStatus.BotStatusModel?> GetInstanceStatusAsync(int port)
    {
        if (port is < 1024 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1024 and 65535");

        try
        {
            using var httpClient = CreateAuthenticatedClient();
            var response = await httpClient.GetAsync($"http://localhost:{port}/botapi/BotStatus");

            if (!response.IsSuccessStatusCode)
                return null;

            var actuResponse = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<BotStatus.BotStatusModel>(
                actuResponse, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }
            );
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get status for instance on port {Port}", port);
            return null;
        }
    }

    /// <summary>
    /// Continuously monitors the health of all registered bot instances.
    /// Updates their active status and last status update timestamp.
    /// </summary>
    /// <returns>A task that completes when monitoring is stopped.</returns>
    private async Task MonitorInstancesAsync()
    {
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));
        do
        {
            try
            {
                await using var db = await provider.GetContextAsync();
                var instances = await db.BotInstances.ToListAsync();

                foreach (var instance in instances)
                {
                    var status = await GetInstanceStatusAsync(instance.Port);
                    instance.IsActive = status != null;
                    instance.LastStatusUpdate = DateTime.UtcNow;
                }

                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during instance monitoring");
            }
        } while (await timer.WaitForNextTickAsync());
    }

    /// <summary>
    /// Gets all active bot instances.
    /// </summary>
    /// <returns>A list of active bot instances.</returns>
    public async Task<List<BotInstance>> GetActiveInstancesAsync()
    {
        await using var db = await provider.GetContextAsync();
        return await db.BotInstances
            .Where(x => x.IsActive)
            .OrderBy(x => x.Port)
            .ToListAsync();
    }

    /// <summary>
    /// Removes a bot instance from the registry.
    /// </summary>
    /// <param name="port">The port number of the instance to remove.</param>
    /// <returns>True if the instance was removed, false if it wasn't found.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when port number is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when not running on master instance.</exception>
    public async Task<bool> RemoveInstanceAsync(int port)
    {
        if (!new BotCredentials().IsMasterInstance)
            throw new InvalidOperationException("Can only remove instances from master bot.");

        if (port is < 1024 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1024 and 65535");

        await using var db = await provider.GetContextAsync();
        var instance = await db.BotInstances.FirstOrDefaultAsync(x => x.Port == port);

        if (instance == null)
            return false;

        db.BotInstances.Remove(instance);
        await db.SaveChangesAsync();
        return true;
    }
}
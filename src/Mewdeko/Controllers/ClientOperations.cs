using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace Mewdeko.Controllers;

/// <summary>
///     Api endpoint for operations done via the discord client, such as getting roles, users, guilds
/// </summary>
[ApiController]
[Route("botapi/[controller]")]
[Authorize("ApiKeyPolicy")]
public class ClientOperations(DiscordShardedClient client) : Controller
{
    /// <summary>
    ///     Used for getting a specific channel type in the api
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ChannelType
    {
        /// <summary>
        ///     For text channels
        /// </summary>
        Text,

        /// <summary>
        ///     For voice channels
        /// </summary>
        Voice,

        /// <summary>
        ///     For category channels
        /// </summary>
        Category,

        /// <summary>
        ///     FOr announcement channels
        /// </summary>
        Announcement,

        /// <summary>
        ///     None
        /// </summary>
        None
    }

    private static readonly JsonSerializerOptions options = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    /// <summary>
    ///     Returns roles for a guild
    /// </summary>
    /// <param name="guildId">The guildid to check for roles</param>
    /// <returns>A 404 if the guildid doesnt exist in the bot, or a collection of roles</returns>
    [HttpGet("roles/{guildId}")]
    public async Task<IActionResult> GetRoles(ulong guildId)
    {
        await Task.CompletedTask;
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound();


        return Ok(guild.Roles.Select(x => new NeededRoleInfo
        {
            Id = x.Id, Name = x.Name
        }));
    }

    /// <summary>
    ///     Gets channels of a specific type from a guildId
    /// </summary>
    /// <param name="guildId">The guild id to get channels from</param>
    /// <param name="channelType">A <see cref="ChannelType" /> for filtering</param>
    /// <returns>Channels based on the filter or 404 if the guild is not found</returns>
    [HttpGet("channels/{guildId}/{channelType}")]
    public async Task<IActionResult> GetChannels(ulong guildId, ChannelType channelType = ChannelType.None)
    {
        await Task.CompletedTask;
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound();

        var channels = channelType switch
        {
            ChannelType.Text => guild.Channels
                .Where(x => x is ITextChannel)
                .Select(c => new NeededRoleInfo
                {
                    Id = c.Id,
                    Name = c.Name
                }),
            ChannelType.Voice => guild.Channels
                .Where(x => x is IVoiceChannel)
                .Select(c => new NeededRoleInfo
                {
                    Id = c.Id,
                    Name = c.Name
                }),
            // ... similar for other types
            _ => guild.Channels
                .Select(c => new NeededRoleInfo
                {
                    Id = c.Id,
                    Name = c.Name
                })
        };

        return Ok(channels);
    }

    /// <summary>
    ///     Gets all IGuildUsers for a guild.
    /// </summary>
    /// <param name="guildId">The guildId to get the users for</param>
    /// <returns>404 if guild not found or the users if found.</returns>
    [HttpGet("users/{guildId}")]
    public async Task<IActionResult> GetUsers(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound();

        var users = await guild.GetUsersAsync().FlattenAsync();
        return Ok(JsonSerializer.Serialize(users, options));
    }


    /// <summary>
    /// Gets text channels from a guild
    /// </summary>
    /// <param name="guildId">The guild id to get text channels from</param>
    /// <returns>Text channels or 404 if the guild is not found</returns>
    [HttpGet("textchannels/{guildId}")]
    public async Task<IActionResult> GetTextChannels(ulong guildId)
    {
        await Task.CompletedTask;
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound();

        var channels = guild.TextChannels.Select(x => new
        {
            Id = x.Id,
            Name = x.Name
        });

        return Ok(channels);
    }

    /// <summary>
    ///     Gets a single user from a guild.
    /// </summary>
    /// <param name="guildId">The guildId to get the users for</param>
    /// <returns>404 if guild not found or the users if found.</returns>
    [HttpGet("user/{guildId}/{userId}")]
    public async Task<IActionResult> GetUser(ulong guildId, ulong userId)
    {
        await Task.CompletedTask;
        var guild = client.Guilds.FirstOrDefault(x => x.Id == guildId);
        if (guild == null)
            return NotFound();

        var user = guild.Users.FirstOrDefault(x => x.Id == userId);
        if (user == null)
            return NotFound();
        var partial = new
        {
            UserId = user.Id, user.Username, AvatarUrl = user.GetAvatarUrl()
        };
        return Ok(partial);
    }

    /// <summary>
    ///     Gets a list of guilds the bot and user have mutual
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    [HttpGet("mutualguilds/{userId}")]
    public async Task<IActionResult> GetMutualAdminGuilds(ulong userId)
    {
        var guilds = client.Guilds;
        var mutuals = guilds
            .Where(x => x.Users.Any(y => y.Id == userId && y.GuildPermissions.Has(GuildPermission.Administrator)))
            .Select(g => new
            {

                id = g.Id,
                name = g.Name,
                icon = g.IconId,  // This will be the icon hash
                owner = g.OwnerId == userId,
                permissions = (int)g.GetUser(userId).GuildPermissions.RawValue,
                features =  Enum.GetValues(typeof(GuildFeature)).Cast<GuildFeature>().Where(x => g.Features.Value.HasFlag(x))
            })
            .ToList();

        if (mutuals.Count!=0)
            return Ok(mutuals);
        return NotFound();
    }

    /// <summary>
    ///     Gets the guilds the bot is in
    /// </summary>
    /// <returns>A list of guildIds  the bot is in</returns>
    [HttpGet("guilds")]
    public async Task<IActionResult> GetGuilds()
    {
        await Task.CompletedTask;
        return Ok(JsonSerializer.Serialize(client.Guilds.Select(x => x.Id), options));
    }

    /// <summary>
    ///     To avoid stupid errors
    /// </summary>
    public class NeededRoleInfo
    {
        /// <summary>
        ///     Name
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        ///     And badge number
        /// </summary>
        [JsonPropertyName("id")]
        public ulong Id { get; set; }
    }
}
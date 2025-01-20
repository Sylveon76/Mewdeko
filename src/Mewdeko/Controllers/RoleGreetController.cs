using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mewdeko.Modules.RoleGreets.Services;

namespace Mewdeko.Controllers;

/// <summary>
/// Controller for managing role-based greetings
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class RoleGreetController : Controller
{
    private readonly RoleGreetService roleGreetService;
    private readonly DiscordShardedClient client;

    /// <summary>
    /// Initializes a new instance of the RoleGreetController
    /// </summary>
    public RoleGreetController(RoleGreetService roleGreetService, DiscordShardedClient client)
    {
        this.roleGreetService = roleGreetService;
        this.client = client;
    }

    /// <summary>
    /// Gets all role greets for a specific role
    /// </summary>
    [HttpGet("role/{roleId}")]
    public async Task<IActionResult> GetGreetsForRole(ulong guildId, ulong roleId)
    {
        var greets = await roleGreetService.GetGreets(roleId);
        return Ok(greets);
    }

    /// <summary>
    /// Gets all role greets in a guild
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllGreets(ulong guildId)
    {
        var greets = await roleGreetService.GetListGreets(guildId);
        return Ok(greets);
    }

    /// <summary>
    /// Adds a new role greet
    /// </summary>
    [HttpPost("role/{roleId}")]
    public async Task<IActionResult> AddRoleGreet(ulong guildId, ulong roleId, [FromBody] ulong channelId)
    {
        var success = await roleGreetService.AddRoleGreet(guildId, channelId, roleId);
        if (!success)
            return BadRequest("Maximum number of greets reached for this role");

        return Ok();
    }

    /// <summary>
    /// Updates the message for a role greet
    /// </summary>
    [HttpPut("{greetId}/message")]
    public async Task<IActionResult> UpdateMessage(ulong guildId, int greetId, [FromBody] string message)
    {
        var greets = await roleGreetService.GetListGreets(guildId);
        var greet = greets.ElementAtOrDefault(greetId - 1);
        if (greet == null)
            return NotFound("Role greet not found");

        await roleGreetService.ChangeMgMessage(greet, message);
        return Ok();
    }

    /// <summary>
    /// Sets the deletion time for a role greet message
    /// </summary>
    [HttpPut("{greetId}/delete-time")]
    public async Task<IActionResult> UpdateDeleteTime(ulong guildId, int greetId, [FromBody] int seconds)
    {
        var greets = await roleGreetService.GetListGreets(guildId);
        var greet = greets.ElementAtOrDefault(greetId - 1);
        if (greet == null)
            return NotFound("Role greet not found");

        await roleGreetService.ChangeRgDelete(greet, seconds);
        return Ok();
    }

    /// <summary>
    /// Updates the webhook for a role greet
    /// </summary>
    [HttpPut("{greetId}/webhook")]
    public async Task<IActionResult> UpdateWebhook(ulong guildId, int greetId, [FromBody] WebhookUpdateRequestRole request)
    {
        var greets = await roleGreetService.GetListGreets(guildId);
        var greet = greets.ElementAtOrDefault(greetId - 1);
        if (greet == null)
            return NotFound("Role greet not found");

        if (request.WebhookUrl == null)
        {
            await roleGreetService.ChangeMgWebhook(greet, null);
            return Ok();
        }

        await roleGreetService.ChangeMgWebhook(greet, request.WebhookUrl);
        return Ok();
    }

    /// <summary>
    /// Enables or disables greeting bots for a role greet
    /// </summary>
    [HttpPut("{greetId}/greet-bots")]
    public async Task<IActionResult> UpdateGreetBots(ulong guildId, int greetId, [FromBody] bool enabled)
    {
        var greets = await roleGreetService.GetListGreets(guildId);
        var greet = greets.ElementAtOrDefault(greetId - 1);
        if (greet == null)
            return NotFound("Role greet not found");

        await roleGreetService.ChangeRgGb(greet, enabled);
        return Ok();
    }

    /// <summary>
    /// Enables or disables a role greet
    /// </summary>
    [HttpPut("{greetId}/disable")]
    public async Task<IActionResult> DisableRoleGreet(ulong guildId, int greetId, [FromBody] bool disabled)
    {
        var greets = await roleGreetService.GetListGreets(guildId);
        var greet = greets.ElementAtOrDefault(greetId - 1);
        if (greet == null)
            return NotFound("Role greet not found");

        await roleGreetService.RoleGreetDisable(greet, disabled);
        return Ok();
    }
}

/// <summary>
/// Webhook update request
/// </summary>
public class WebhookUpdateRequestRole
{
    /// <summary>
    /// ze url
    /// </summary>
    public string? WebhookUrl { get; set; }
}
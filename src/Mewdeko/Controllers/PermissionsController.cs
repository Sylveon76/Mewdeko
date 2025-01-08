using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Help;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services.Impl;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for setting permissions for commands and triggers
/// </summary>
[ApiController]
[Route("botapi/[controller]")]
[Authorize("ApiKeyPolicy")]
public class PermissionsController(
    PermissionService permissionService,
    DiscordPermOverrideService dpoService,
    CommandService cmdServ) : Controller
{
    /// <summary>
    ///     Gets all dpos for a guild
    /// </summary>
    /// <param name="guildId">the guild to get permissions for</param>
    /// <returns></returns>
    [HttpGet("dpo/{guildId}")]
    public async Task<IActionResult> GetPermissionOverridesForGuildAsync(ulong guildId)
    {
        var overrides = await dpoService.GetAllOverrides(guildId);
        return Ok(overrides);
    }

    /// <summary>
    ///     Add a discord permission override
    /// </summary>
    /// <param name="guildId"></param>
    /// <param name="commandName"></param>
    /// <param name="permissions"></param>
    /// <returns></returns>
    [HttpPost("dpo/{guildId}")]
    public async Task<IActionResult> AddDpo(ulong guildId, [FromBody] DpoRequest request)
    {
        var com = cmdServ.Search(request.Command);
        if (!com.IsSuccess)
            return BadRequest(com);
        var perms = (GuildPermission)request.Permissions;
        var over = await dpoService.AddOverride(guildId, request.Command, perms);
        return Ok(over);
    }

    /// <summary>
    ///     Remove a dpo
    /// </summary>
    /// <param name="guildId"></param>
    /// <param name="commandName"></param>
    /// <returns></returns>
    [HttpDelete("dpo/{guildId}")]
    public async Task<IActionResult> RemoveDpo(ulong guildId, [FromBody] string commandName)
    {
        var com = cmdServ.Search(commandName);
        if (!com.IsSuccess)
            return BadRequest(com);
        await dpoService.RemoveOverride(guildId, commandName);
        return Ok();
    }

    /// <summary>
    ///     Gets regular permissions for a guild
    /// </summary>
    /// <param name="guildId">the guild to get permissions for</param>
    /// <returns></returns>
    [HttpGet("regular/{guildId}")]
    public async Task<IActionResult> GetPermissionsForGuildAsync(ulong guildId)
    {
        var perms = await permissionService.GetCacheFor(guildId);
        return Ok(perms);
    }

    /// <summary>
    /// Adds a new permission setting for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="permission">The permission configuration to add</param>
    /// <returns>Success response when added</returns>
    [HttpPost("regular/{guildId}")]
    public async Task<IActionResult> AddPermission(ulong guildId, [FromBody] Permissionv2 permission)
    {
        await permissionService.AddPermissions(guildId, permission);
        return Ok();
    }

    /// <summary>
    /// Removes a permission setting by its index
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="index">The index of the permission to remove</param>
    /// <returns>Success response when removed</returns>
    [HttpDelete("regular/{guildId}/{index}")]
    public async Task<IActionResult> RemovePermission(ulong guildId, int index)
    {
        await permissionService.RemovePerm(guildId, index);
        return Ok();
    }

    /// <summary>
    /// Moves a permission setting to a new position
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="request">The move request containing from and to indices</param>
    /// <returns>Success response when moved</returns>
    [HttpPost("regular/{guildId}/move")]
    public async Task<IActionResult> MovePermission(ulong guildId, [FromBody] MovePermRequest request)
    {
        await permissionService.UnsafeMovePerm(guildId, request.From, request.To);
        return Ok();
    }

    /// <summary>
    /// Resets all permission settings for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>Success response when reset</returns>
    [HttpPost("regular/{guildId}/reset")]
    public async Task<IActionResult> ResetPermissions(ulong guildId)
    {
        await permissionService.Reset(guildId);
        return Ok();
    }

    /// <summary>
    /// Sets the verbose mode for permission feedback
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="verbose">Whether to enable verbose mode</param>
    /// <returns>Success response when updated</returns>
    [HttpPost("regular/{guildId}/verbose")]
    public async Task<IActionResult> SetVerbose(ulong guildId, [FromBody] bool verbose)
    {
        permissionService.UpdateCache(new GuildConfig
        {
            GuildId = guildId,
            VerbosePermissions = verbose
        });
        return Ok();
    }

    /// <summary>
    /// Sets the permission role for the guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="roleId">The ID of the role to set as permission role</param>
    /// <returns>Success response when updated</returns>
    [HttpPost("regular/{guildId}/role")]
    public async Task<IActionResult> SetPermissionRole(ulong guildId, [FromBody] string roleId)
    {
        permissionService.UpdateCache(new GuildConfig
        {
            GuildId = guildId,
            PermissionRole = roleId
        });
        return Ok();
    }

    /// <summary>
    /// Gets all commands and modules with their information
    /// </summary>
    /// <returns>A dictionary of module names and their commands</returns>
    [HttpGet("commands")]
    public async Task<IActionResult> GetCommandsAndModules()
    {
        try
        {
            var modules = cmdServ.Modules;
            var moduleList = new List<Module>();

            foreach (var module in modules)
            {
                var moduleName = module.IsSubmodule ? module.Parent!.Name : module.Name;

                var commands = module.Commands.OrderByDescending(x => x.Name)
                    .Select(cmd =>
                    {
                        var userPerm = cmd.Preconditions
                            .FirstOrDefault(ca => ca is UserPermAttribute) as UserPermAttribute;
                        var botPerm = cmd.Preconditions
                            .FirstOrDefault(ca => ca is BotPermAttribute) as BotPermAttribute;
                        var isDragon = cmd.Preconditions
                            .FirstOrDefault(ca => ca is RequireDragonAttribute) as RequireDragonAttribute;

                        return new Command
                        {
                            BotVersion = StatsService.BotVersion,
                            CommandName = cmd.Aliases.Any() ? cmd.Aliases[0] : cmd.Name,
                            Description = cmd.Summary ?? "No description available",
                            Example = cmd.Remarks?.Split('\n').ToList() ?? new List<string>(),
                            GuildUserPermissions = userPerm?.UserPermissionAttribute.GuildPermission?.ToString() ?? "",
                            ChannelUserPermissions =
                                userPerm?.UserPermissionAttribute.ChannelPermission?.ToString() ?? "",
                            GuildBotPermissions = botPerm?.GuildPermission?.ToString() ?? "",
                            ChannelBotPermissions = botPerm?.ChannelPermission?.ToString() ?? "",
                            IsDragon = isDragon != null
                        };
                    })
                    .ToList();

                moduleList.Add(new Module(commands, moduleName));
            }

            return Ok(moduleList);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error fetching commands and modules");
            return StatusCode(500, "Error fetching commands and modules");
        }
    }

    /// <summary>
    ///     E
    /// </summary>
    public class DpoRequest
    {
        /// <summary>
        ///     e
        /// </summary>
        public string Command { get; set; }

        /// <summary>
        ///     e
        /// </summary>
        public ulong Permissions { get; set; }
    }

    /// <summary>
    /// Request model for moving permissions
    /// </summary>
    public class MovePermRequest
    {
        /// <summary>
        /// The source index to move from
        /// </summary>
        public int From { get; set; }

        /// <summary>
        /// The destination index to move to
        /// </summary>
        public int To { get; set; }
    }
}
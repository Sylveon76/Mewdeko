﻿using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Mewdeko.Controllers;

/// <summary>
/// Controller for managing guild configs via the api
/// </summary>
/// <param name="service"></param>
[ApiController]
[Route("api/[controller]/{guildId}")]
public class GuildConfigController(GuildSettingsService service) : Controller
{
    /// <summary>
    /// Gets a guild config
    /// </summary>
    /// <param name="guildId">The guildid to get a config for</param>
    /// <returns></returns>
    [HttpGet]
    public async Task<IActionResult> GetGuildConfig(ulong guildId)
    {
        try
        {
            var config = await service.GetGuildConfig(guildId, x => x.IncludeEverything());
            return Ok(config);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error getting guild config");
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Updates a guild config from the provided json and guildid
    /// </summary>
    /// <param name="guildId">The guildid to update a config for</param>
    /// <param name="model">The json body of the model to update</param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> UpdateGuildConfig(ulong guildId, [FromBody] GuildConfig model)
    {
        try
        {
            await service.UpdateGuildConfig(guildId, model);
            return Ok();
        }
        catch (Exception e)
        {
            Log.Error(e, "Error updating guild config");
            return StatusCode(500);
        }
    }
}
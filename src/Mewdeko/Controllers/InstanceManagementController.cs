using Mewdeko.Modules.OwnerOnly.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Mewdeko.Controllers;

/// <summary>
/// Controller for managing multiple bot instances.
/// </summary>
[ApiController]
[Route("botapi/[controller]")]
[Authorize("ApiKeyPolicy")]
public class InstanceManagementController : Controller
{
    private readonly InstanceManagementService instanceManagementService;
    private readonly ILogger<InstanceManagementController> logger;

    /// <summary>
    /// Initializes a new instance of the controller.
    /// </summary>
    public InstanceManagementController(
        InstanceManagementService instanceManagementService,
        ILogger<InstanceManagementController> logger)
    {
        this.instanceManagementService = instanceManagementService;
        this.logger = logger;
    }

    /// <summary>
    /// Gets all active bot instances.
    /// </summary>
    /// <returns>List of active bot instances.</returns>
    [HttpGet]
    public async Task<IActionResult> GetInstances()
    {
        try
        {
            var instances = await instanceManagementService.GetActiveInstancesAsync();
            return Ok(instances);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get instances");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Adds a new bot instance.
    /// </summary>
    /// <param name="port">The port number the instance is running on.</param>
    [HttpPost("{port}")]
    public async Task<IActionResult> AddInstance(int port)
    {
        try
        {
            var result = await instanceManagementService.AddInstanceAsync(port);
            if (!result.Success)
                return BadRequest("Failed to add instance");

            return Ok(result.Status);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add instance on port {Port}", port);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Removes a bot instance.
    /// </summary>
    /// <param name="port">The port number of the instance to remove.</param>
    [HttpDelete("{port}")]
    public async Task<IActionResult> RemoveInstance(int port)
    {
        try
        {
            var result = await instanceManagementService.RemoveInstanceAsync(port);
            if (!result)
                return NotFound();

            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove instance on port {Port}", port);
            return StatusCode(500, "Internal server error");
        }
    }
}
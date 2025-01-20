using System.IO;
using Mewdeko.Modules.Utility.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for managing join and leave statistics
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class JoinLeaveController : Controller
{
    private readonly JoinLeaveLoggerService joinLeaveService;

    /// <summary>
    ///     Creates a new instance of the JoinLeaveController
    /// </summary>
    /// <param name="joinLeaveService">Service for managing join/leave analytics</param>
    public JoinLeaveController(JoinLeaveLoggerService joinLeaveService)
    {
        this.joinLeaveService = joinLeaveService;
    }

    /// <summary>
    ///     Gets join statistics for the specified guild
    /// </summary>
    /// <param name="guildId">The ID of the guild to get statistics for</param>
    /// <returns>Join statistics and graph data</returns>
    [HttpGet("join-stats")]
    public async Task<ActionResult<GraphStatsResponse>> GetJoinStats(ulong guildId)
    {
        var dailyLogs = await joinLeaveService.GetGroupedJoinLeaveDataAsync(guildId, true);

        var peakDay = dailyLogs.OrderByDescending(log => log.Count).FirstOrDefault();
        var total = dailyLogs.Sum(log => log.Count);
        var average = dailyLogs.Count > 0 ? dailyLogs.Average(log => log.Count) : 0;

        return Ok(new GraphStatsResponse
        {
            DailyStats = dailyLogs.Select(log => new DailyStatDto
            {
                Date = log.Date, Count = log.Count
            }).ToList(),
            Summary = new GraphSummaryDto
            {
                Total = total,
                Average = average,
                PeakDate = peakDay?.Date ?? DateTime.UtcNow,
                PeakCount = peakDay?.Count ?? 0
            }
        });
    }

    /// <summary>
    ///     Gets leave statistics for the specified guild
    /// </summary>
    /// <param name="guildId">The ID of the guild to get statistics for</param>
    /// <returns>Leave statistics and graph data</returns>
    [HttpGet("leave-stats")]
    public async Task<ActionResult<GraphStatsResponse>> GetLeaveStats(ulong guildId)
    {
        var dailyLogs = await joinLeaveService.GetGroupedJoinLeaveDataAsync(guildId, false);

        var peakDay = dailyLogs.OrderByDescending(log => log.Count).FirstOrDefault();
        var total = dailyLogs.Sum(log => log.Count);
        var average = dailyLogs.Count > 0 ? dailyLogs.Average(log => log.Count) : 0;

        return Ok(new GraphStatsResponse
        {
            DailyStats = dailyLogs.Select(log => new DailyStatDto
            {
                Date = log.Date, Count = log.Count
            }).ToList(),
            Summary = new GraphSummaryDto
            {
                Total = total,
                Average = average,
                PeakDate = peakDay?.Date ?? DateTime.UtcNow,
                PeakCount = peakDay?.Count ?? 0
            }
        });
    }

    /// <summary>
    ///     Gets the average joins per guild
    /// </summary>
    /// <param name="guildId">The ID of the guild to check</param>
    /// <returns>Average number of joins</returns>
    [HttpGet("average-joins")]
    public async Task<IActionResult> GetAverageJoins(ulong guildId)
    {
        var average = await joinLeaveService.CalculateAverageJoinsPerGuildAsync(guildId);
        return Ok(average);
    }

    /// <summary>
    ///     Gets a graph of join statistics
    /// </summary>
    /// <param name="guildId">The ID of the guild to get statistics for</param>
    /// <returns>A tuple containing the graph image and embed data</returns>
    [HttpGet("join-graph")]
    public async Task<IActionResult> GetJoinGraph(ulong guildId)
    {
        var (imageStream, embed) = await joinLeaveService.GenerateJoinGraphAsync(guildId);
        return Ok(new
        {
            ImageData = Convert.ToBase64String(((MemoryStream)imageStream).ToArray()), Embed = embed
        });
    }

    /// <summary>
    ///     Gets a graph of leave statistics
    /// </summary>
    /// <param name="guildId">The ID of the guild to get statistics for</param>
    /// <returns>A tuple containing the graph image and embed data</returns>
    [HttpGet("leave-graph")]
    public async Task<IActionResult> GetLeaveGraph(ulong guildId)
    {
        var (imageStream, embed) = await joinLeaveService.GenerateLeaveGraphAsync(guildId);
        return Ok(new
        {
            ImageData = Convert.ToBase64String(((MemoryStream)imageStream).ToArray()), Embed = embed
        });
    }

    /// <summary>
    ///     Sets the join graph color for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild to update</param>
    /// <param name="color">The color value to set</param>
    /// <returns>Success response</returns>
    [HttpPost("join-color")]
    public async Task<IActionResult> SetJoinColor(ulong guildId, [FromBody] uint color)
    {
        await joinLeaveService.SetJoinColorAsync(color, guildId);
        return Ok();
    }

    /// <summary>
    ///     Sets the leave graph color for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild to update</param>
    /// <param name="color">The color value to set</param>
    /// <returns>Success response</returns>
    [HttpPost("leave-color")]
    public async Task<IActionResult> SetLeaveColor(ulong guildId, [FromBody] uint color)
    {
        await joinLeaveService.SetLeaveColorAsync(color, guildId);
        return Ok();
    }


    /// <summary>
    ///     Response model containing join/leave graph and statistics data
    /// </summary>
    public class GraphStatsResponse
    {
        /// <summary>
        ///     Data points for each day in the graph
        /// </summary>
        public List<DailyStatDto> DailyStats { get; set; }

        /// <summary>
        ///     Summary statistics for the timespan
        /// </summary>
        public GraphSummaryDto Summary { get; set; }
    }

    /// <summary>
    ///     Single data point for a specific day
    /// </summary>
    public class DailyStatDto
    {
        /// <summary>
        ///     The date of this data point
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        ///     Number of events on this date
        /// </summary>
        public int Count { get; set; }
    }

    /// <summary>
    ///     Summary statistics for the graph period
    /// </summary>
    public class GraphSummaryDto
    {
        /// <summary>
        ///     Total number of events in the period
        /// </summary>
        public int Total { get; set; }

        /// <summary>
        ///     Average events per day
        /// </summary>
        public double Average { get; set; }

        /// <summary>
        ///     Date with highest number of events
        /// </summary>
        public DateTime PeakDate { get; set; }

        /// <summary>
        ///     Number of events on the peak date
        /// </summary>
        public int PeakCount { get; set; }
    }
}
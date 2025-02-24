using LinqToDB.Mapping;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Database.Models;

/// <summary>
///     Represents the configuration for AI services in a guild.
/// </summary>
[Table("GuildAiConfig")]
public class GuildAiConfig : DbEntity
{
    /// <summary>
    ///     Gets or sets the ID of the guild this configuration belongs to.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the AI service is enabled for this guild.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     Gets or sets the API key for the AI service.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    ///     Gets or sets the AI provider being used.
    /// </summary>
    public AiService.AiProvider Provider { get; set; }

    /// <summary>
    ///     Gets or sets the model ID for the AI service.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    ///     Gets or sets the channel ID where the AI service is active.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the system prompt for the AI service.
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    ///     Gets or sets the total number of tokens used by this guild.
    /// </summary>
    public int TokensUsed { get; set; }

    /// <summary>
    ///     Gets or sets the custom embed used for the ai model.
    /// </summary>
    public string? CustomEmbed { get; set; }

    /// <summary>
    ///     Gets or sets the webhook url used for sending ai messages.
    /// </summary>
    public string? WebhookUrl { get; set; }
}
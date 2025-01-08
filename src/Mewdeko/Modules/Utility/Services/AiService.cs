using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mewdeko.Common.Configs;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Utility.Services.Impl;
using Mewdeko.Services.Strings;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Service that handles Ai-related functionality across different providers.
/// </summary>
public class AiService : INService
{
    private readonly DbContextProvider dbProvider;
    private readonly DiscordShardedClient client;
    private readonly GeneratedBotStrings strings;
    private readonly BotConfig botConfig;
    private readonly AiClientFactory aiClientFactory;
    private readonly IHttpClientFactory httpFactory;
    private readonly ConcurrentDictionary<AiProvider, List<AiModel>> modelCache;
    private readonly TimeSpan modelCacheExpiry = TimeSpan.FromHours(24);
    private DateTime lastModelUpdate = DateTime.MinValue;

    /// <summary>
    ///     Defines the available AI providers.
    /// </summary>
    public enum AiProvider
    {
        /// <summary>
        ///     OpenAI's API provider.
        /// </summary>
        OpenAi,

        /// <summary>
        ///     Groq's API provider.
        /// </summary>
        Groq,

        /// <summary>
        ///     Anthropic's Claude API provider.
        /// </summary>
        Claude
    }

    /// <summary>
    ///     Represents an Ai model with its metadata.
    /// </summary>
    public class AiModel
    {
        /// <summary>
        ///     Gets or sets the model identifier.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        ///     Gets or sets the display name of the model.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     Gets or sets the provider of this model.
        /// </summary>
        public AiProvider Provider { get; set; }
    }

    private static readonly List<AiModel> SupportedModels = new()
    {
        new AiModel
        {
            Id = "gpt-4-turbo", Name = "GPT-4 Turbo", Provider = AiProvider.OpenAi
        },
        new AiModel
        {
            Id = "gpt-3.5-turbo", Name = "GPT-3.5 Turbo", Provider = AiProvider.OpenAi
        },
        new AiModel
        {
            Id = "claude-3-opus-20240229", Name = "Claude 3 Opus", Provider = AiProvider.Claude
        },
        new AiModel
        {
            Id = "claude-3-sonnet-20240229", Name = "Claude 3 Sonnet", Provider = AiProvider.Claude
        },
        new AiModel
        {
            Id = "mixtral-8x7b", Name = "Mixtral 8x7B", Provider = AiProvider.Groq
        },
        new AiModel
        {
            Id = "llama2-70b", Name = "Llama 2 70B", Provider = AiProvider.Groq
        }
    };

    /// <summary>
    ///     Initializes a new instance of the <see cref="AiService" /> class.
    /// </summary>
    public AiService(DbContextProvider dbProvider, IHttpClientFactory httpFactory,
        GeneratedBotStrings strings, BotConfig config, EventHandler handler, DiscordShardedClient client)
    {
        this.dbProvider = dbProvider;
        this.httpFactory = httpFactory;
        this.strings = strings;
        botConfig = config;
        this.client = client;
        aiClientFactory = new AiClientFactory();
        handler.MessageReceived += HandleMessage;
        modelCache = new ConcurrentDictionary<AiProvider, List<AiModel>>();
    }


    /// <summary>
    ///     Gets or creates an Ai configuration for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>The guild's Ai configuration.</returns>
    public async Task<GuildAiConfig> GetOrCreateConfig(ulong guildId)
    {
        await using var db = await dbProvider.GetContextAsync();
        return await db.GuildAiConfig.FirstOrDefaultAsync(x => x.GuildId == guildId)
               ?? new GuildAiConfig
               {
                   GuildId = guildId
               };
    }

    /// <summary>
    ///     Updates or creates a guild's Ai configuration.
    /// </summary>
    /// <param name="config">The configuration to update.</param>
    public async Task UpdateConfig(GuildAiConfig config)
    {
        await using var db = await dbProvider.GetContextAsync();
        if (config.Id == 0)
            db.GuildAiConfig.Add(config);
        else
            db.GuildAiConfig.Update(config);
        await db.SaveChangesAsync();
    }

    /// <summary>
    ///     Sets a custom embed template for AI responses in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to set the custom embed for.</param>
    /// <param name="customEmbed">The embed template, which can include %airesponse% placeholder.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetCustomEmbed(ulong guildId, string customEmbed)
    {
        var config = await GetOrCreateConfig(guildId);
        config.CustomEmbed = customEmbed;
        await UpdateConfig(config);
    }

    // Service method
    /// <summary>
    ///     Sets the webhook URL for AI responses in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="webhookUrl">The webhook URL, or null to disable webhooks.</param>
    public async Task SetWebhook(ulong guildId, string? webhookUrl)
    {
        var config = await GetOrCreateConfig(guildId);
        config.WebhookUrl = webhookUrl;
        await UpdateConfig(config);
    }

    private async Task HandleMessage(SocketMessage msg)
    {
        if (msg is not IUserMessage || msg.Author.IsBot) return;
        if (msg.Channel is not IGuildChannel guildChannel) return;

        var config = await GetOrCreateConfig(guildChannel.GuildId);
        if (!config.Enabled || config.ChannelId != msg.Channel.Id) return;

        if (msg.Content == "deletesession")
        {
            await ClearConversation(guildChannel.GuildId, msg.Author.Id);
            await msg.Channel.SendConfirmAsync(strings.AiConversationDeleted(guildChannel.GuildId));
            return;
        }

        if (string.IsNullOrEmpty(config.ApiKey))
        {
            await msg.Channel.SendErrorAsync(strings.AiNoApiKey(guildChannel.GuildId, config.Provider), botConfig);
            return;
        }

        DiscordWebhookClient? webhook = null;
        ulong? webhookMessageId = null;
        if (!string.IsNullOrEmpty(config.WebhookUrl))
        {
            webhook = new DiscordWebhookClient(config.WebhookUrl);
            webhookMessageId = await webhook.SendMessageAsync(embeds:
            [
                new EmbedBuilder()
                    .WithOkColor()
                    .WithDescription(strings.AiProcessingRequest(guildChannel.GuildId, msg.Author.Mention))
                    .Build()
            ]);
        }
        else
        {
            await msg.Channel.SendConfirmAsync(strings.AiProcessingRequest(guildChannel.GuildId, msg.Author.Mention));
        }

        try
        {
            await StreamResponse(config, webhookMessageId, msg, webhook);
            await UpdateConfig(config);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error in AI processing");
            if (webhook != null && webhookMessageId.HasValue)
            {
                await webhook.ModifyMessageAsync(webhookMessageId.Value, x =>
                {
                    x.Embeds = new[]
                    {
                        new EmbedBuilder()
                            .WithErrorColor()
                            .WithDescription(strings.AiErrorOccurred(guildChannel.GuildId, ex.Message))
                            .Build()
                    };
                });
            }
            else
            {
                await msg.Channel.SendErrorAsync(strings.AiErrorOccurred(guildChannel.GuildId, ex.Message), botConfig);
            }
        }
        finally
        {
            webhook?.Dispose();
        }
    }

    private async Task ClearConversation(ulong guildId, ulong userId)
    {
        await using var db = await dbProvider.GetContextAsync();
        var conversation = await db.AiConversations
            .Include(x => x.Messages)
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId);

        if (conversation != null)
        {
            db.AiMessages.RemoveRange(conversation.Messages);
            db.AiConversations.Remove(conversation);
            await db.SaveChangesAsync();
        }
    }

    private async Task StreamResponse(GuildAiConfig config, ulong? webhookMessageId, SocketMessage userMsg,
        DiscordWebhookClient? webhook)
    {
        await using var db = await dbProvider.GetContextAsync();
        var conversation = await db.AiConversations
            .Include(x => x.Messages)
            .FirstOrDefaultAsync(x => x.GuildId == config.GuildId && x.UserId == userMsg.Author.Id);

        var guildChannel = userMsg.Channel as SocketTextChannel;
        var replacer = new ReplacementBuilder()
            .WithChannel(userMsg.Channel)
            .WithUser(userMsg.Author)
            .WithServer(client, guildChannel.Guild)
            .WithClient(client)
            .Build();

        var sysPrompt = replacer.Replace(config.SystemPrompt);

        if (conversation == null)
        {
            conversation = new AiConversation
            {
                GuildId = config.GuildId,
                UserId = userMsg.Author.Id,
                Messages =
                [
                    new AiMessage
                    {
                        Role = "system", Content = sysPrompt ?? ""
                    }
                ]
            };
            db.AiConversations.Add(conversation);
        }

        conversation.Messages.Add(new AiMessage
        {
            Role = "user", Content = userMsg.Content
        });
        await db.SaveChangesAsync();

        var (aiClient, _) = aiClientFactory.Create(config.Provider);
        var responseBuilder = new StringBuilder();
        var lastUpdate = DateTime.UtcNow;
        var tokenCount = 0;

        replacer = new ReplacementBuilder()
            .WithChannel(userMsg.Channel)
            .WithUser(userMsg.Author)
            .WithServer(client, guildChannel.Guild)
            .WithClient(client)
            .WithOverride("%airesponse%", () => responseBuilder.ToString())
            .Build();

        var stream = await aiClient.StreamResponseAsync(conversation.Messages, config.Model, config.ApiKey);
        await foreach (var text in stream)
        {
            if (string.IsNullOrEmpty(text)) continue;
            responseBuilder.Append(text);
            if (!((DateTime.UtcNow - lastUpdate).TotalSeconds >= 1)) continue;
            lastUpdate = DateTime.UtcNow;
            await UpdateMessageEmbed();
        }

        conversation.Messages.Add(new AiMessage
        {
            Role = "assistant", Content = responseBuilder.ToString()
        });
        config.TokensUsed += tokenCount;

        await db.SaveChangesAsync();
        await UpdateMessageEmbed();

        if (conversation.Messages.Count > 10)
        {
            var toRemove = conversation.Messages.Take(conversation.Messages.Count - 10);
            db.AiMessages.RemoveRange(toRemove);
            await db.SaveChangesAsync();
        }

        async Task UpdateMessageEmbed()
        {
            if (webhook != null && webhookMessageId.HasValue)
            {
                var content = string.IsNullOrEmpty(config.CustomEmbed)
                    ? responseBuilder.ToString()
                    : replacer.Replace(config.CustomEmbed);

                content = content.EscapeWeirdStuff();

                if (SmartEmbed.TryParse(content, config.GuildId, out var embedData, out var plainText, out _))
                {
                    var modifiedEmbeds = embedData.Select(embed =>
                    {
                        var builder = embed.ToEmbedBuilder();
                        if (builder.Footer is null)
                        {
                            builder.WithFooter(strings.AiResponseFooter(config.GuildId, userMsg.Author.Username, tokenCount));
                        }
                        return builder.Build();
                    }).ToList();

                    await webhook.ModifyMessageAsync(webhookMessageId.Value, x =>
                    {
                        x.Content = plainText;
                        x.Embeds = modifiedEmbeds;
                    });
                }
                else
                {
                    // For non-embed content, maintain formatting but escape properly
                    await webhook.ModifyMessageAsync(webhookMessageId.Value, x =>
                    {
                        x.Content = null;
                        x.Embeds = new List<Embed>
                        {
                            new EmbedBuilder()
                                .WithOkColor()
                                .WithDescription(content)
                                .WithFooter(strings.AiResponseFooter(config.GuildId, userMsg.Author.Username, tokenCount))
                                .Build()
                        };
                    });
                }
                return;
            }

            await userMsg.Channel.SendConfirmAsync(responseBuilder.ToString());
        }
    }

    /// <summary>
    ///     Retrieves a list of AI models supported by the specified provider.
    /// </summary>
    /// <param name="provider">The AI provider to fetch models from (OpenAI, Groq, or Claude).</param>
    /// <param name="apiKey">The API key used to authenticate with the provider.</param>
    /// <returns>A list of supported AI models for the specified provider.</returns>
    /// <remarks>
    ///     This method caches results for 24 hours to minimize API calls. Models are fetched from:
    ///     - OpenAI: api.openai.com/v1/models
    ///     - Groq: api.groq.com/v1/models
    ///     - Claude: api.anthropic.com/v1/models
    /// </remarks>
    /// <exception cref="NotSupportedException">Thrown when an unsupported provider is specified.</exception>
    /// <exception cref="HttpRequestException">Thrown when the API request fails.</exception>
    public async Task<List<AiModel>> GetSupportedModels(AiProvider provider, string apiKey)
    {
        if (modelCache.TryGetValue(provider, out var models) &&
            DateTime.UtcNow - lastModelUpdate < modelCacheExpiry)
        {
            return models;
        }

        using var http = httpFactory.CreateClient();
        models = provider switch
        {
            AiProvider.OpenAi => await FetchOpenAiModels(http, apiKey),
            AiProvider.Groq => await FetchGroqModels(http, apiKey),
            AiProvider.Claude => await FetchClaudeModels(http, apiKey),
            _ => throw new NotSupportedException($"Provider {provider} not supported")
        };

        modelCache.AddOrUpdate(provider, models, (_, _) => models);
        lastModelUpdate = DateTime.UtcNow;
        return models;
    }

    private async Task<List<AiModel>> FetchOpenAiModels(HttpClient http, string apiKey)
    {
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        var response = await http.GetFromJsonAsync<OpenAiModelsResponse>("https://api.openai.com/v1/models");

        return response?.Data
            .Where(m => m.Id.StartsWith("gpt"))
            .Select(m => new AiModel
            {
                Id = m.Id, Name = FormatModelName(m.Id), Provider = AiProvider.OpenAi
            })
            .ToList() ?? new List<AiModel>();
    }

    private async Task<List<AiModel>> FetchGroqModels(HttpClient http, string apiKey)
    {
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        var response = await http.GetFromJsonAsync<GroqModelsResponse>("https://api.groq.com/v1/models");

        return response?.Data
            .Select(m => new AiModel
            {
                Id = m.Id, Name = FormatModelName(m.Id), Provider = AiProvider.Groq
            })
            .ToList() ?? new List<AiModel>();
    }

    private async Task<List<AiModel>> FetchClaudeModels(HttpClient http, string apiKey)
    {
        http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        var response = await http.GetStringAsync("https://api.anthropic.com/v1/models");
        var data = JsonSerializer.Deserialize<ClaudeModelsResponse>(response);
        Log.Information(response);

        return data.Data
            .Select(m => new AiModel
            {
                Id = m.Id, Name = FormatModelName(m.Id), Provider = AiProvider.Claude
            })
            .ToList();
    }

    private static string FormatModelName(string modelId)
    {
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
            modelId.Replace('-', ' ')
                .Replace('/', ' ')
                .Replace('_', ' '));
    }

    private record class OpenAiModelsResponse(List<OpenAiModel> Data);

    private record class OpenAiModel(string Id);

    private record class GroqModelsResponse(List<GroqModel> Data);

    private record class GroqModel(string Id);

    /// <summary>
    ///     Represents a Claude AI model from Anthropic's API.
    /// </summary>
    public class ClaudeModel
    {
        /// <summary>
        ///     Gets or sets the type identifier for this model. Always "model".
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>
        ///     Gets or sets the unique identifier for this model (e.g. "claude-3-opus-20240229").
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        ///     Gets or sets the human-readable name for this model (e.g. "Claude 3 Opus").
        /// </summary>
        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        /// <summary>
        ///     Gets or sets the UTC timestamp when this model version was created.
        /// </summary>
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    ///     Represents the response from Anthropic's models endpoint containing available Claude models.
    /// </summary>
    public class ClaudeModelsResponse
    {
        /// <summary>
        ///     Gets or sets the list of available Claude models.
        /// </summary>
        [JsonPropertyName("data")]
        public List<ClaudeModel> Data { get; set; }

        /// <summary>
        ///     Gets or sets whether there are additional models beyond this page of results.
        /// </summary>
        [JsonPropertyName("has_more")]
        public bool HasMore { get; set; }

        /// <summary>
        ///     Gets or sets the ID of the first model in this response.
        /// </summary>
        [JsonPropertyName("first_id")]
        public string FirstId { get; set; }

        /// <summary>
        ///     Gets or sets the ID of the last model in this response.
        /// </summary>
        [JsonPropertyName("last_id")]
        public string LastId { get; set; }
    }
}
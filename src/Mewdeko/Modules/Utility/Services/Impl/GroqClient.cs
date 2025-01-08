using System.Text.Json.Nodes;
using System.Threading;
using GroqApiLibrary;

namespace Mewdeko.Modules.Utility.Services.Impl;

/// <summary>
///     Groq API client implementation using GroqApiLibrary.
/// </summary>
public class GroqClient : IAiClient
{
    /// <summary>
    ///     Gets the AI provider type for this client.
    /// </summary>
    public AiService.AiProvider Provider
    {
        get
        {
            return AiService.AiProvider.Groq;
        }
    }

    /// <summary>
    ///     Streams a response from the Groq AI model.
    /// </summary>
    /// <param name="messages">The conversation history.</param>
    /// <param name="model">The model identifier to use.</param>
    /// <param name="apiKey">The API key for authentication.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A stream containing the AI response.</returns>
    public async Task<IAsyncEnumerable<string>> StreamResponseAsync(IEnumerable<AiMessage> messages, string model,
        string apiKey, CancellationToken cancellationToken = default)
    {
        var client = new GroqApiClient(apiKey);

        var request = new JsonObject
        {
            ["model"] = model,
            ["messages"] = new JsonArray(messages.Select(m => new JsonObject
            {
                ["role"] = m.Role, ["content"] = m.Content
            }).ToArray()),
            ["stream"] = true
        };

        var stream = client.CreateChatCompletionStreamAsync(request);

        return stream.Select(update =>
            update?["choices"]?[0]?["delta"]?["content"]?.GetValue<string>() ?? "");
    }
}
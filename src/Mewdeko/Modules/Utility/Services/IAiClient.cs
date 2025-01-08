using System.IO;
using System.Threading;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
/// Defines the interface for AI service clients.
/// </summary>
public interface IAiClient
{
    /// <summary>
    /// Gets the AI provider type for this client.
    /// </summary>
    AiService.AiProvider Provider { get; }

    /// <summary>
    /// Streams a response from the AI model.
    /// </summary>
    /// <param name="messages">The conversation history.</param>
    /// <param name="model">The model identifier to use.</param>
    /// <param name="apiKey">The API key for authentication.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A stream containing the AI response.</returns>
    Task<IAsyncEnumerable<string>> StreamResponseAsync(IEnumerable<AiMessage> messages, string model, string apiKey, CancellationToken cancellationToken = default);

}
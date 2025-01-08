using System.Threading;
using Claudia;
using Serilog;
using RequestOptions = Claudia.RequestOptions;

namespace Mewdeko.Modules.Utility.Services.Impl;

/// <summary>
///     Implements Claude AI functionality using the Claudia library.
/// </summary>
public class ClaudeClient : IAiClient
{
    /// <summary>
    ///     Gets the AI provider type for this client.
    /// </summary>
    public AiService.AiProvider Provider
    {
        get
        {
            return AiService.AiProvider.Claude;
        }
    }

    /// <summary>
    ///     Streams a response from the Claude AI model.
    /// </summary>
    /// <param name="messages">The conversation history.</param>
    /// <param name="model">The model identifier to use.</param>
    /// <param name="apiKey">The API key for authentication.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A stream containing the AI response.</returns>
    public async Task<IAsyncEnumerable<string>> StreamResponseAsync(IEnumerable<AiMessage> messages, string model,
        string apiKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = new Anthropic
            {
                ApiKey = apiKey
            };

            var systemMessage = messages.FirstOrDefault(m => m.Role == "system")?.Content;
            var filteredMessages = messages.Where(m => m.Role != "system");

            var stream = client.Messages.CreateStreamAsync(new MessageRequest
            {
                Model = model,
                MaxTokens = 1024,
                System = systemMessage,
                Messages = filteredMessages.Select(m => new Message { Role = m.Role, Content = m.Content }).ToArray()
            }, cancellationToken: cancellationToken);

            return stream.Where(e => e is ContentBlockDelta)
                .Cast<ContentBlockDelta>()
                .Select(c => c.Delta.Text);
        }
        catch (Exception e)
        {
            Log.Error(e, "An error occured while streaming messages.");
            throw;
        }
    }
}
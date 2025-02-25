using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using Serilog;

namespace Mewdeko.Modules.Utility.Services.Impl;

/// <summary>
///     Implements Groq AI functionality using direct API calls.
/// </summary>
public class GroqClient : IAiClient
{
    /// <summary>
    ///     Gets the AI provider type for this client.
    /// </summary>
    public AiService.AiProvider Provider => AiService.AiProvider.Groq;

    /// <summary>
    ///     Streams a response from the Groq AI model.
    /// </summary>
    /// <param name="messages">The conversation history.</param>
    /// <param name="model">The model identifier to use.</param>
    /// <param name="apiKey">The API key for authentication.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A stream containing the raw JSON responses from the Groq API.</returns>
    public async Task<IAsyncEnumerable<string>> StreamResponseAsync(IEnumerable<AiMessage> messages, string model,
        string apiKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            // Prepare the request body
            var requestBody = new
            {
                model,
                messages = messages.Select(m => new
                {
                    role = m.Role,
                    content = m.Content
                }).ToArray(),
                stream = true,
                max_tokens = 1024  // Similar to Claude's MaxTokens
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            // Create and send the request
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions")
            {
                Content = content
            };

            var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            // Create a channel to stream the responses
            var channel = System.Threading.Channels.Channel.CreateUnbounded<string>();

            // Process the stream in a separate task
            _ = Task.Run(async () =>
            {
                try
                {
                    using var reader = new StreamReader(stream);

                    while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                    {
                        var line = await reader.ReadLineAsync();
                        if (string.IsNullOrEmpty(line))
                            continue;

                        if (line.StartsWith("data: "))
                        {
                            var data = line.Substring("data: ".Length);

                            // The stream ends with "data: [DONE]"
                            if (data == "[DONE]")
                                break;

                            // Write the raw JSON to the channel
                            await channel.Writer.WriteAsync(data, cancellationToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error processing Groq stream");
                }
                finally
                {
                    channel.Writer.Complete();
                    httpClient.Dispose();
                }
            }, cancellationToken);

            // Return the channel reader as an IAsyncEnumerable
            return channel.Reader.ReadAllAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while streaming messages from Groq.");
            throw;
        }
    }
}
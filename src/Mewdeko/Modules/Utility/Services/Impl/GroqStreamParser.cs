using System.Text.Json;
using Serilog;

namespace Mewdeko.Modules.Utility.Services.Impl;

/// <summary>
///     Parses streaming responses from Groq's API.
/// </summary>
public class GroqStreamParser : IAiStreamParser
{
    /// <inheritdoc />
    public string ParseDelta(string json, AiService.AiProvider provider)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Extract the content delta from the JSON
            // Format is {"id":"chatcmpl-...","object":"chat.completion.chunk","created":1234,"model":"llama-..","choices":[{"index":0,"delta":{"content":"..."},"logprobs":null,"finish_reason":null}]}
            if (root.TryGetProperty("choices", out var choices) &&
                choices.GetArrayLength() > 0 &&
                choices[0].TryGetProperty("delta", out var delta) &&
                delta.TryGetProperty("content", out var content))
            {
                return content.GetString() ?? "";
            }

            return "";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error parsing Groq delta: {Json}", json);
            return "";
        }
    }

    /// <inheritdoc />
    public (int InputTokens, int OutputTokens, int TotalTokens)? ParseUsage(string json, AiService.AiProvider provider)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Extract usage information if present
            if (root.TryGetProperty("usage", out var usage))
            {
                var promptTokens = 0;
                var completionTokens = 0;
                var totalTokens = 0;

                if (usage.TryGetProperty("prompt_tokens", out var prompt))
                    promptTokens = prompt.GetInt32();

                // Groq uses completion_tokens, not completion_token
                if (usage.TryGetProperty("completion_tokens", out var completion))
                    completionTokens = completion.GetInt32();

                if (usage.TryGetProperty("total_tokens", out var total))
                    totalTokens = total.GetInt32();
                else
                    totalTokens = promptTokens + completionTokens;

                return (promptTokens, completionTokens, totalTokens);
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error parsing Groq usage: {Json}", json);
            return null;
        }
    }

    /// <inheritdoc />
    public bool IsStreamFinished(string json, AiService.AiProvider provider)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Check for finish_reason which indicates completion
            if (root.TryGetProperty("choices", out var choices) &&
                choices.GetArrayLength() > 0 &&
                choices[0].TryGetProperty("finish_reason", out var finishReason))
            {
                // "stop" is the normal completion reason for Groq
                var reason = finishReason.ValueKind == JsonValueKind.Null ? null : finishReason.GetString();
                return !string.IsNullOrEmpty(reason);
            }

            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking if Groq stream finished: {Json}", json);
            return false;
        }
    }
}
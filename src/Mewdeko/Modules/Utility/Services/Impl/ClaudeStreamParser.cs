using System.Text.Json;
using Mewdeko.Modules.Utility.Common;

namespace Mewdeko.Modules.Utility.Services.Impl;

/// <summary>
/// Parses streaming responses from Claude's API.
/// </summary>
public class ClaudeStreamParser : IAiStreamParser
{
    /// <inheritdoc />
    public string ParseDelta(string json, AiService.AiProvider provider)
    {
        try
        {
            var response = JsonSerializer.Deserialize<ClaudeStreamResponse>(json);
            return response?.Content?.FirstOrDefault()?.Text ?? "";
        }
        catch
        {
            return "";
        }
    }

    /// <inheritdoc />
    public (int InputTokens, int OutputTokens, int TotalTokens)? ParseUsage(string json, AiService.AiProvider provider)
    {
        try
        {
            var response = JsonSerializer.Deserialize<ClaudeStreamResponse>(json);
            if (response?.Usage == null) return null;

            return (
                response.Usage.InputTokens,
                response.Usage.OutputTokens,
                response.Usage.InputTokens + response.Usage.OutputTokens
            );
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public bool IsStreamFinished(string json, AiService.AiProvider provider)
    {
        try
        {
            var response = JsonSerializer.Deserialize<ClaudeStreamResponse>(json);
            return response?.StopReason == "end_turn";
        }
        catch
        {
            return false;
        }
    }
}
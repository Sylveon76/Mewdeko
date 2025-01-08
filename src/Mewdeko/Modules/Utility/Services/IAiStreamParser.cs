namespace Mewdeko.Modules.Utility.Services;

/// <summary>
/// Interface for parsing streaming responses from AI providers.
/// </summary>
public interface IAiStreamParser
{
    /// <summary>
    /// Parses a delta update from the stream.
    /// </summary>
    /// <param name="json">The JSON response from the AI provider.</param>
    /// <param name="provider">The AI provider type.</param>
    /// <returns>The parsed text delta.</returns>
    string ParseDelta(string json, AiService.AiProvider provider);

    /// <summary>
    /// Parses token usage information from the stream.
    /// </summary>
    /// <param name="json">The JSON response from the AI provider.</param>
    /// <param name="provider">The AI provider type.</param>
    /// <returns>A tuple containing input, output, and total token counts.</returns>
    (int InputTokens, int OutputTokens, int TotalTokens)? ParseUsage(string json, AiService.AiProvider provider);

    /// <summary>
    /// Determines if the stream has finished.
    /// </summary>
    /// <param name="json">The JSON response from the AI provider.</param>
    /// <param name="provider">The AI provider type.</param>
    /// <returns>True if the stream is finished, false otherwise.</returns>
    bool IsStreamFinished(string json, AiService.AiProvider provider);
}
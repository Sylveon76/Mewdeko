namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Factory for creating AI clients.
/// </summary>
public interface IAiClientFactory
{
    /// <summary>
    /// Creates an AI client and stream parser for the specified provider.
    /// </summary>
    /// <param name="provider">The AI provider to create components for.</param>
    /// <returns>A tuple containing the AI client and its corresponding stream parser.</returns>
    /// <exception cref="NotSupportedException">Thrown when the specified provider is not supported.</exception>
    public (IAiClient Client, IAiStreamParser Parser) Create(AiService.AiProvider provider);
}
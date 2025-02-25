namespace Mewdeko.Modules.Utility.Services.Impl;

/// <summary>
/// Factory for creating AI clients and their corresponding stream parsers.
/// </summary>
public class AiClientFactory : IAiClientFactory
{
    private readonly Dictionary<AiService.AiProvider, (IAiClient Client, IAiStreamParser Parser)> clients = new()
    {
        [AiService.AiProvider.Claude] = (new ClaudeClient(), new ClaudeStreamParser()),
        [AiService.AiProvider.Groq] = (new GroqClient(), new GroqStreamParser())
    };

    /// <inheritdoc />
    public (IAiClient Client, IAiStreamParser Parser) Create(AiService.AiProvider provider)
    {
        if (!clients.TryGetValue(provider, out var client))
            throw new NotSupportedException($"Provider {provider} not supported");

        return client;
    }
}
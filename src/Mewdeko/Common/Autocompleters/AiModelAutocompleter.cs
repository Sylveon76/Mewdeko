using Discord.Interactions;
using Mewdeko.Modules.Utility.Services;
using Serilog;

namespace Mewdeko.Common.Autocompleters;

/// <summary>
/// Provides autocomplete suggestions for AI model selection.
/// </summary>
public class AiModelAutoCompleter : AutocompleteHandler
{
    private readonly AiService aiService;

    /// <summary>
    /// Initializes a new instance of the AiModelAutoCompleter class.
    /// </summary>
    public AiModelAutoCompleter(AiService aiService)
    {
        this.aiService = aiService;
    }

    /// <inheritdoc />
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var guildId = context.Guild?.Id ?? 0;
        if (guildId == 0)
            return AutocompletionResult.FromError(new Exception("Must be used in a guild"));

        var firstOption = autocompleteInteraction.Data.Options.FirstOrDefault(x => x.Name == "provider");

        var provider = (string)firstOption?.Value switch
        {
            "Claude" => AiService.AiProvider.Claude,
            "OpenAI" => AiService.AiProvider.OpenAi,
            "Groq" => AiService.AiProvider.Groq,
            _ => AiService.AiProvider.Claude
        };
        Log.Information(provider.GetType().Name);

        var config = await aiService.GetOrCreateConfig(guildId);

        if (string.IsNullOrEmpty(config.ApiKey))
            return AutocompletionResult.FromSuccess(
                [new AutocompleteResult("No API key set for this provider", "none")]);

        try
        {
            var models = await aiService.GetSupportedModels(provider, config.ApiKey);
            var searchTerm = (string)autocompleteInteraction.Data.Current.Value;

            return AutocompletionResult.FromSuccess(
                models.Where(m => m.Id.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                                  m.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(m => m.Id.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase))
                    .Take(25)
                    .Select(m => new AutocompleteResult(m.Name, m.Id)));
        }
        catch
        {
            return AutocompletionResult.FromSuccess(
                [new AutocompleteResult("Invalid API key for this provider", "none")]);
        }
    }
}
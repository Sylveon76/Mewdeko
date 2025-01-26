using Discord.Interactions;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Starboard.Services;

namespace Mewdeko.Common.Autocompleters;

/// <summary>
///     Autocompleter for starboard configurations.
/// </summary>
public class StarboardAutocompleter : AutocompleteHandler
{
    private const int MaxSuggestions = 25;

    /// <summary>
    ///     Initializes a new instance of the StarboardAutocompleter class.
    /// </summary>
    /// <param name="starboard">The StarboardService.</param>
    /// <param name="credentials">The bot credentials.</param>
    public StarboardAutocompleter(StarboardService starboard, IBotCredentials credentials)
    {
        Starboard = starboard;
        Credentials = credentials;
    }

    /// <summary>
    ///     Gets or sets the StarboardService.
    /// </summary>
    private StarboardService Starboard { get; }

    /// <summary>
    ///     Gets or sets the bot credentials.
    /// </summary>
    private IBotCredentials Credentials { get; }

    /// <summary>
    ///     Generates suggestions for autocomplete.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="autocompleteInteraction">The autocomplete interaction.</param>
    /// <param name="parameter">The parameter info.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the autocomplete result.</returns>
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        var input = (autocompleteInteraction.Data.Current.Value as string ?? "").ToLowerInvariant();

        var suggestions = Starboard.GetStarboards(context.Guild.Id)
            .Where(x => x.Emote.Contains(input, StringComparison.OrdinalIgnoreCase))
            .Take(MaxSuggestions)
            .Select(x => new AutocompleteResult($"ID: {x.Id} - {x.Emote}", x.Id));

        return AutocompletionResult.FromSuccess(suggestions);
    }
}
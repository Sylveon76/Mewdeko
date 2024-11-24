using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Highlights.Services;

namespace Mewdeko.Modules.Highlights;

/// <summary>
///     Slash module for managing highlights.
/// </summary>
[Group("highlights", "Set or manage highlights")]
public class SlashHighlights : MewdekoSlashModuleBase<HighlightsService>
{
    private readonly DbContextProvider dbProvider;
    private readonly InteractiveService interactivity;

    /// <summary>
    ///     Initializes a new instance of <see cref="SlashHighlights" />.
    /// </summary>
    /// <param name="interactivity">Embed pagination service</param>
    /// <param name="db">The database provider</param>
    public SlashHighlights(InteractiveService interactivity, DbContextProvider dbProvider)
    {
        this.interactivity = interactivity;
        this.dbProvider = dbProvider;
    }

    /// <summary>
    ///     Adds a new highlight.
    /// </summary>
    /// <param name="words">Word or regex to add</param>
    [SlashCommand("add", "Add new highlights.")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task AddHighlight([Summary("words", "Words to highlight.")] string words)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var highlights = (await dbContext.Highlights.ForUser(ctx.Guild.Id, ctx.User.Id)).ToList();
        if (string.IsNullOrWhiteSpace(words))
        {
            await ctx.Interaction.SendErrorAsync(Strings.HighlightPhraseRequired(ctx.Guild.Id), Config)
                .ConfigureAwait(false);
            return;
        }

        if (highlights.Count > 0 && highlights.Select(x => x.Word.ToLower()).Contains(words.ToLower()))
        {
            await ctx.Interaction.SendErrorAsync(Strings.HighlightAlreadyExists(ctx.Guild.Id), Config)
                .ConfigureAwait(false);
        }
        else
        {
            await Service.AddHighlight(ctx.Guild.Id, ctx.User.Id, words).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync(Strings.HighlightAdded(ctx.Guild.Id, Format.Code(words)))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Lists the current highlights.
    /// </summary>
    [SlashCommand("list", "List your current highlights.")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task ListHighlights()
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var highlightsForUser = (await dbContext.Highlights.ForUser(ctx.Guild.Id, ctx.User.Id)).ToList();

        if (highlightsForUser.Count == 0)
        {
            await ctx.Interaction.SendErrorAsync(Strings.HighlightNoHighlights(ctx.Guild.Id), Config)
                .ConfigureAwait(false);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(highlightsForUser.Count() / 10)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, ctx.Interaction as SocketInteraction,
            TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            var highlightsEnumerable = highlightsForUser.Skip(page * 10).Take(10);
            return new PageBuilder().WithOkColor()
                .WithTitle(Strings.HighlightListTitle(ctx.Guild.Id, highlightsForUser.Count()))
                .WithDescription(string.Join("\n",
                    highlightsEnumerable.Select(x => $"{highlightsForUser.IndexOf(x) + 1}. {x.Word}")));
        }
    }

    /// <summary>
    ///     Deletes a highlight.
    /// </summary>
    /// <param name="words">Autocomplete list of highlights to delete</param>
    [SlashCommand("delete", "Delete a highlight.")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task DeleteHighlight(
        [Autocomplete(typeof(HighlightAutocompleter))] [Summary("words", "The highlight to delete.")]
        string words)
    {
        if (string.IsNullOrWhiteSpace(words))
        {
            await ctx.Interaction.SendErrorAsync(Strings.HighlightEmptyDelete(ctx.Guild.Id), Config)
                .ConfigureAwait(false);
            return;
        }

        await using var dbContext = await dbProvider.GetContextAsync();

        var highlightsForUser = await dbContext.Highlights.ForUser(ctx.Guild.Id, ctx.User.Id);

        if (highlightsForUser.Count == 0)
        {
            await ctx.Interaction.SendErrorAsync(Strings.HighlightCannotDelete(ctx.Guild.Id), Config);

            return;
        }

        if (int.TryParse(words, out var number))
        {
            var todelete = highlightsForUser.ElementAt(number - 1);
            if (todelete is null)
            {
                await ctx.Interaction.SendErrorAsync(Strings.HighlightNotExist(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            await Service.RemoveHighlight(todelete).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync(Strings.HighlightRemoved(ctx.Guild.Id, Format.Code(words)))
                .ConfigureAwait(false);
            return;
        }

        if (!highlightsForUser.Select(x => x.Word).Contains(words))
        {
            await ctx.Interaction.SendErrorAsync(Strings.HighlightNotExist(ctx.Guild.Id), Config)
                .ConfigureAwait(false);
            return;
        }

        await Service.RemoveHighlight(highlightsForUser.Find(x => x.Word == words)).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync(Strings.HighlightRemoved(ctx.Guild.Id, Format.Code(words)))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Attempts to match a highlight in a given message.
    /// </summary>
    /// <param name="words">The phrase to match</param>
    [SlashCommand("match", "Find a matching highlight.")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task MatchHighlight(
        [Autocomplete(typeof(HighlightAutocompleter))] [Summary("words", "The highlight to find.")]
        string words)
    {
        if (string.IsNullOrWhiteSpace(words))
        {
            await ctx.Interaction.SendErrorAsync(Strings.HighlightEmptyMatch(ctx.Guild.Id), Config)
                .ConfigureAwait(false);
            return;
        }

        await using var dbContext = await dbProvider.GetContextAsync();

        var highlightsForUser = await dbContext.Highlights.ForUser(ctx.Guild.Id, ctx.User.Id);

        var matched = highlightsForUser.Where(x => words.ToLower().Contains(x.Word.ToLower()));
        if (!matched.Any())
        {
            await ctx.Interaction.SendErrorAsync(Strings.HighlightNoMatches(ctx.Guild.Id), Config)
                .ConfigureAwait(false);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory1)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(matched.Count() / 10)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, Context.Channel,
            TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory1(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            var highlightsEnumerable = matched.Skip(page * 10).Take(10);
            return new PageBuilder().WithOkColor()
                .WithTitle(Strings.HighlightListTitle(ctx.Guild.Id, highlightsForUser.Count()))
                .WithDescription(string.Join("\n",
                    highlightsEnumerable.Select(x => $"{highlightsForUser.IndexOf(x) + 1}. {x.Word}")));
        }
    }

    /// <summary>
    ///     Toggles a user to be ignored.
    /// </summary>
    /// <param name="user">User to be ignored</param>
    [SlashCommand("toggle-user", "Ignore a specified user.")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task ToggleUser(IUser user)
    {
        var enabled = await Service.ToggleIgnoredUser(ctx.Guild.Id, ctx.User.Id, user.Id.ToString())
            .ConfigureAwait(false);

        await ctx.Interaction.SendConfirmAsync(
                enabled
                    ? Strings.HighlightIgnoredUserAdded(ctx.Guild.Id, user.Mention)
                    : Strings.HighlightIgnoredUserRemoved(ctx.Guild.Id, user.Mention))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Toggles a channel to be ignored.
    /// </summary>
    /// <param name="channel">The channel to be toggled</param>
    [SlashCommand("toggle-channel", "Ignore a specified channel.")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task ToggleChannel(ITextChannel channel)
    {
        var enabled = await Service.ToggleIgnoredUser(ctx.Guild.Id, ctx.User.Id, channel.Id.ToString())
            .ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync(
                enabled
                    ? Strings.HighlightIgnoredChannelAdded(ctx.Guild.Id, channel.Mention)
                    : Strings.HighlightIgnoredChannelRemoved(ctx.Guild.Id, channel.Mention))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Toggles highlights globally.
    /// </summary>
    /// <param name="enabled"></param>
    [SlashCommand("toggle-global", "Enable or disable highlights globally.")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task ToggleGlobal([Summary("enabled", "Are highlights enabled globally?")] bool enabled)
    {
        await Service.ToggleHighlights(ctx.Guild.Id, ctx.User.Id, enabled).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync(enabled
            ? Strings.HighlightEnabled(ctx.Guild.Id)
            : Strings.HighlightDisabled(ctx.Guild.Id));
    }
}
using Discord.Interactions;
using MartineApiNet;
using MartineApiNet.Enums;
using Mewdeko.Modules.Searches.Services;
using Refit;
using Serilog;

namespace Mewdeko.Modules.Searches;

/// <summary>
///     Provides slash command interactions for searching and retrieving content from various sources.
/// </summary>
public class SlashSearches(MartineApi martineApi) : MewdekoSlashModuleBase<SearchesService>
{
    /// <summary>
    ///     Handles the "randomimage" component interaction, fetching and displaying a new random image from the specified
    ///     category.
    /// </summary>
    /// <param name="tag">The category of image to fetch.</param>
    /// <param name="userId">The Discord user ID who initiated the interaction.</param>
    /// <remarks>
    ///     This interaction command fetches a new random image from the specified category via the Martine API.
    ///     It supports ephemerality, showing the response only to the initiating user.
    /// </remarks>
    [ComponentInteraction("randomimage:*.*", true)]
    public async Task RandomImageButton(SearchesService.ImageTag tag, string userId)
    {
        await DeferAsync().ConfigureAwait(false);
        ulong.TryParse(userId, out var id);

        try
        {
            var image = await Service.GetRandomImageAsync(tag).ConfigureAwait(false);
            var button = new ComponentBuilder().WithButton("Another!", $"randomimage:{tag}.{ctx.User.Id}");

            var em = new EmbedBuilder()
                .WithOkColor()
                .WithAuthor($"u/{image.Data.Author.Name}")
                .WithDescription($"Title: {image.Data.Title}\n[Source]({image.Data.PostUrl})")
                .WithFooter($"{image.Data.Upvotes} Upvotes! | r/{image.Data.Subreddit.Name} Powered by martineAPI")
                .WithImageUrl(image.Data.ImageUrl);

            if (ctx.User.Id != id)
            {
                await ctx.Interaction.FollowupAsync(
                    embed: em.Build(),
                    components: button.Build(),
                    ephemeral: true
                ).ConfigureAwait(false);
                return;
            }

            await ctx.Interaction.ModifyOriginalResponseAsync(x =>
            {
                x.Embed = em.Build();
                x.Components = button.Build();
            }).ConfigureAwait(false);
        }
        catch (ApiException ex)
        {
            Log.Error(
                "Image fetch failed in button handler. Error:\nCode: {StatusCode}\nContent: {Content}",
                ex.StatusCode,
                ex.HasContent ? ex.Content : "No Content"
            );

            var errorEmbed = new EmbedBuilder()
                .WithErrorColor()
                .WithDescription("Failed to fetch image, please try again later!");

            if (ctx.User.Id != id)
            {
                await ctx.Interaction.FollowupAsync(
                    embed: errorEmbed.Build(),
                    ephemeral: true
                ).ConfigureAwait(false);
                return;
            }

            await ctx.Interaction.ModifyOriginalResponseAsync(x =>
            {
                x.Embed = errorEmbed.Build();
                x.Components = new ComponentBuilder().Build(); // Remove the button on error
            }).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Handles the "meme" component interaction, fetching and showing a random meme.
    /// </summary>
    /// <param name="userid">The Discord user ID who initiated the meme fetch interaction.</param>
    /// <remarks>
    ///     This interaction command fetches a random meme from the configured sources via the Martine API
    ///     and presents it to the user who triggered the interaction.
    ///     The command supports ephemerality, showing the response only to the initiating user.
    /// </remarks>
    [ComponentInteraction("meme:*", true)]
    public async Task Meme(string userid)
    {
        await DeferAsync().ConfigureAwait(false);
        ulong.TryParse(userid, out var id);
        var image = await martineApi.RedditApi.GetRandomMeme(Toptype.year).ConfigureAwait(false);
        var em = new EmbedBuilder
        {
            Author = new EmbedAuthorBuilder
            {
                Name = $"u/{image.Data.Author.Name}"
            },
            Description = $"Title: {image.Data.Title}\n[Source]({image.Data.PostUrl})",
            Footer = new EmbedFooterBuilder
            {
                Text =
                    $"{image.Data.Upvotes} Upvotes {image.Data.Downvotes} Downvotes | r/{image.Data.Subreddit.Name} | Powered by MartineApi"
            },
            ImageUrl = image.Data.ImageUrl,
            Color = Mewdeko.OkColor
        };
        if (ctx.User.Id != id)
        {
            await ctx.Interaction.FollowupAsync(embed: em.Build(), ephemeral: true).ConfigureAwait(false);
            return;
        }

        await ctx.Interaction.ModifyOriginalResponseAsync(x => x.Embed = em.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Handles the "randomreddit" component interaction, fetching and displaying a random post from a specified subreddit.
    /// </summary>
    /// <param name="subreddit">The subreddit from which to fetch a random post.</param>
    /// <param name="userId">The Discord user ID who initiated the subreddit fetch interaction.</param>
    /// <remarks>
    ///     This interaction command fetches a random post from the specified subreddit via the Martine API.
    ///     It supports ephemerality, allowing the response to be visible only to the user who initiated the interaction.
    /// </remarks>
    [ComponentInteraction("randomreddit:*.*", true)]
    public async Task RandomReddit(string subreddit, string userId)
    {
        await DeferAsync().ConfigureAwait(false);
        ulong.TryParse(userId, out var id);

        var image = await martineApi.RedditApi.GetRandomFromSubreddit(subreddit, Toptype.year).ConfigureAwait(false);

        var em = new EmbedBuilder
        {
            Author = new EmbedAuthorBuilder
            {
                Name = $"u/{image.Data.Author.Name}"
            },
            Description = $"Title: {image.Data.Title}\n[Source]({image.Data.PostUrl})",
            Footer = new EmbedFooterBuilder
            {
                Text = $"{image.Data.Upvotes} Upvotes! | r/{image.Data.Subreddit.Name} Powered by martineAPI"
            },
            ImageUrl = image.Data.ImageUrl,
            Color = Mewdeko.OkColor
        };
        if (ctx.User.Id != id)
        {
            await ctx.Interaction.FollowupAsync(embed: em.Build(), ephemeral: true).ConfigureAwait(false);
            return;
        }

        await ctx.Interaction.ModifyOriginalResponseAsync(x => x.Embed = em.Build()).ConfigureAwait(false);
    }
}
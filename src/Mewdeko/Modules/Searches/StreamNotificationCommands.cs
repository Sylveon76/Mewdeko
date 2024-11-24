using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Searches.Services;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Searches;

public partial class Searches
{
    /// <summary>
    ///     Contains commands for managing stream notifications within a Discord guild.
    /// </summary>
    [Group]
    public class StreamNotificationCommands(DbContextProvider dbProvider, InteractiveService serv)
        : MewdekoSubmodule<StreamNotificationService>
    {
        /// <summary>
        ///     Adds a new stream to the notification list for the current guild.
        /// </summary>
        /// <param name="link">The link to the stream to be added.</param>
        /// <remarks>
        ///     This command allows users with the "Manage Messages" permission to add a stream link to the guild's notification
        ///     list.
        ///     When the stream goes live, the guild will be notified. This feature supports various streaming platforms.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task StreamAdd(string link)
        {
            var data = await Service.FollowStream(ctx.Guild.Id, ctx.Channel.Id, link).ConfigureAwait(false);
            if (data is null)
            {
                await ReplyErrorAsync(Strings.StreamNotAdded(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var embed = Service.GetEmbed(ctx.Guild.Id, data);
            await ctx.Channel.EmbedAsync(embed, Strings.StreamTracked(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Removes a stream from the notification list based on its index.
        /// </summary>
        /// <param name="index">The 1-based index of the stream in the notification list to be removed.</param>
        /// <remarks>
        ///     Users with the "Manage Messages" permission can remove streams from the guild's notification list.
        ///     This command requires specifying the index of the stream, which can be obtained through the stream list command.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        [Priority(1)]
        public async Task StreamRemove(int index)
        {
            if (--index < 0)
                return;

            var fs = await Service.UnfollowStreamAsync(ctx.Guild.Id, index).ConfigureAwait(false);
            if (fs is null)
            {
                await ReplyErrorAsync(Strings.StreamNo(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(Strings.StreamRemoved(ctx.Guild.Id,
                Format.Bold(fs.Username),
                fs.Type)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Clears all streams from the guild's notification list.
        /// </summary>
        /// <remarks>
        ///     This command allows administrators to remove all stream links from the guild's notification list.
        ///     It is intended for use in situations where a complete reset of stream notifications is necessary.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task StreamsClear()
        {
            var count = await Service.ClearAllStreams(ctx.Guild.Id).ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.StreamsCleared(ctx.Guild.Id, count)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists all streams currently followed by the guild.
        /// </summary>
        /// <remarks>
        ///     Provides a paginated list of all streams the guild is following for notifications.
        ///     This list includes each stream's username, type, and the channel where notifications are sent.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task StreamList()
        {
            var streams = new List<FollowedStream>();

            await using var dbContext = await dbProvider.GetContextAsync();
            var all = (await dbContext
                    .ForGuildId(ctx.Guild.Id, set => set.Include(gc => gc.FollowedStreams)))
                .FollowedStreams
                .OrderBy(x => x.Id)
                .ToList();

            for (var index = all.Count - 1; index >= 0; index--)
            {
                var fs = all[index];
                if (((SocketGuild)ctx.Guild).GetTextChannel(fs.ChannelId) is null)
                    await Service.UnfollowStreamAsync(fs.GuildId, index).ConfigureAwait(false);
                else
                    streams.Insert(0, fs);
            }

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(streams.Count / 12)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                var elements = streams.Skip(page * 12).Take(12)
                    .ToList();

                if (elements.Count == 0)
                {
                    return new PageBuilder()
                        .WithDescription(Strings.StreamsNone(ctx.Guild.Id))
                        .WithErrorColor();
                }

                var eb = new PageBuilder()
                    .WithTitle(Strings.StreamsFollowTitle(ctx.Guild.Id))
                    .WithOkColor();
                for (var index = 0; index < elements.Count; index++)
                {
                    var elem = elements[index];
                    eb.AddField(
                        $"**#{index + 1 + 12 * page}** {elem.Username.ToLower()}",
                        $"【{elem.Type}】\n<#{elem.ChannelId}>\n{elem.Message?.TrimTo(50)}",
                        true);
                }

                return eb;
            }
        }

        /// <summary>
        ///     Toggles the setting for notifying the guild when a followed stream goes offline.
        /// </summary>
        /// <remarks>
        ///     Users with the "Manage Messages" permission can toggle notifications for when any of the followed streams go
        ///     offline.
        ///     This setting is helpful for guilds that wish to track stream status closely.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task StreamOffline()
        {
            var newValue = await Service.ToggleStreamOffline(ctx.Guild.Id);
            if (newValue)
                await ReplyConfirmAsync(Strings.StreamOffEnabled(ctx.Guild.Id)).ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.StreamOffDisabled(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets a custom notification message for a specific stream in the guild's notification list.
        /// </summary>
        /// <param name="index">The 1-based index of the stream to set the message for.</param>
        /// <param name="message">
        ///     The custom message to be sent when the stream goes live. An empty message will reset it to
        ///     default.
        /// </param>
        /// <remarks>
        ///     This command allows customization of the notification message for specific streams.
        ///     It is useful for adding personalized or additional information to stream notifications.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task StreamMessage(int index, [Remainder] string message)
        {
            if (--index < 0)
                return;

            var (followed, fs) = await Service.SetStreamMessage(ctx.Guild.Id, index, message);

            if (!followed)
            {
                await ReplyConfirmAsync(Strings.StreamNotFollowing(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                await ReplyConfirmAsync(Strings.StreamMessageReset(ctx.Guild.Id, Format.Bold(fs.Username)))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmAsync(Strings.StreamMessageSet(ctx.Guild.Id, Format.Bold(fs.Username)))
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Checks the live status of a stream by its URL.
        /// </summary>
        /// <param name="url">The URL of the stream to check.</param>
        /// <remarks>
        ///     This command is useful for manually checking the live status of a stream.
        ///     It provides immediate feedback on whether the stream is currently live and, if so, the number of viewers.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task StreamCheck(string url)
        {
            try
            {
                var data = await Service.GetStreamDataAsync(url).ConfigureAwait(false);
                if (data is null)
                {
                    await ReplyErrorAsync(Strings.NoChannelFound(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                if (data.IsLive)
                {
                    await ReplyConfirmAsync(Strings.StreamerOnline(ctx.Guild.Id,
                            Format.Bold(data.Name),
                            Format.Bold(data.Viewers.ToString())))
                        .ConfigureAwait(false);
                }
                else
                {
                    await ReplyConfirmAsync(Strings.StreamerOffline(ctx.Guild.Id, data.Name))
                        .ConfigureAwait(false);
                }
            }
            catch
            {
                await ReplyErrorAsync(Strings.NoChannelFound(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }
    }
}
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Modules.Utility.Services;
using Serilog;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    ///     Provides commands for managing message repeaters within the guild.
    ///     Allows for creating, modifying, and removing automated repeating messages.
    /// </summary>
    [Group]
    public class RepeatCommands(InteractiveService interactivity) : MewdekoSubmodule<MessageRepeaterService>
    {
        /// <summary>
        ///     Immediately triggers a repeater by its index number.
        /// </summary>
        /// <param name="index">The one-based index of the repeater to trigger.</param>
        /// <remarks>
        ///     The repeater will execute immediately and then continue on its normal schedule.
        ///     Requires the Manage Messages permission.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task RepeatInvoke(int index)
        {
            if (!Service.RepeaterReady)
                return;

            var repeater = Service.GetRepeaterByIndex(ctx.Guild.Id, index - 1);
            if (repeater == null)
            {
                await ReplyErrorAsync(Strings.RepeatInvokeNone(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            repeater.Reset();
            await repeater.Trigger().ConfigureAwait(false);

            try
            {
                await ctx.Message.AddReactionAsync(new Emoji("🔄")).ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>
        ///     Removes a repeater by its index number.
        /// </summary>
        /// <param name="index">The one-based index of the repeater to remove.</param>
        /// <remarks>
        ///     This action is permanent and cannot be undone.
        ///     Requires the Manage Messages permission.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task RepeatRemove(int index)
        {
            if (!Service.RepeaterReady)
                return;

            var repeater = Service.GetRepeaterByIndex(ctx.Guild.Id, index - 1);
            if (repeater == null)
            {
                await ReplyErrorAsync(Strings.IndexOutOfRange(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var description = GetRepeaterInfoString(repeater);
            await Service.RemoveRepeater(repeater.Repeater);

            await ctx.Channel.EmbedAsync(new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.RepeaterRemoved(ctx.Guild.Id, index))
                .WithDescription(description)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles the redundancy check for a repeater.
        /// </summary>
        /// <param name="index">The one-based index of the repeater to modify.</param>
        /// <remarks>
        ///     When redundancy is enabled, the repeater will not send a message if it's message is the last one in the channel.
        ///     Requires the Manage Messages permission.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task RepeatRedundant(int index)
        {
            if (!Service.RepeaterReady)
                return;

            var repeater = Service.GetRepeaterByIndex(ctx.Guild.Id, index - 1);
            if (repeater == null)
            {
                await ReplyErrorAsync(Strings.IndexOutOfRange(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var success = await Service.ToggleRepeaterRedundancyAsync(ctx.Guild.Id, repeater.Repeater.Id);
            if (!success)
            {
                await ReplyErrorAsync(Strings.RepeatActionFailed(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (repeater.Repeater.NoRedundant)
                await ReplyConfirmAsync(Strings.RepeaterRedundantNo(ctx.Guild.Id, index)).ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.RepeaterRedundantYes(ctx.Guild.Id, index)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Creates a repeater with the specified message.
        /// </summary>
        /// <param name="message">The message to repeat.</param>
        /// <remarks>
        ///     Uses default interval of 5 minutes if not specified.
        ///     Requires the Manage Messages permission.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        [Priority(-1)]
        public Task Repeat([Remainder] string? message)
        {
            return Repeat(null, null, message);
        }

        /// <summary>
        ///     Creates a repeater with specified interval and message.
        /// </summary>
        /// <param name="interval">The time interval between repeats.</param>
        /// <param name="message">The message to repeat.</param>
        /// <remarks>
        ///     Interval must be between 5 seconds and 25000 minutes.
        ///     Requires the Manage Messages permission.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        [Priority(0)]
        public Task Repeat(StoopidTime interval, [Remainder] string? message)
        {
            return Repeat(null, interval, message);
        }

        /// <summary>
        ///     Creates a repeater that runs at a specific time each day.
        /// </summary>
        /// <param name="dt">The time of day to run the repeater.</param>
        /// <param name="message">The message to repeat.</param>
        /// <remarks>
        ///     The repeater will run once per day at the specified time.
        ///     Requires the Manage Messages permission.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        [Priority(1)]
        public Task Repeat(GuildDateTime dt, [Remainder] string? message)
        {
            return Repeat(dt, null, message);
        }

        /// <summary>
        ///     Creates a repeater with optional start time and interval.
        /// </summary>
        /// <param name="dt">Optional time of day to start the repeater.</param>
        /// <param name="interval">Optional interval between repeats.</param>
        /// <param name="message">The message to repeat.</param>
        /// <remarks>
        ///     Most flexible repeat command allowing both time and interval specification.
        ///     Requires the Manage Messages permission.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        [Priority(2)]
        public async Task Repeat(GuildDateTime? dt, StoopidTime? interval, [Remainder] string? message)
        {
            try
            {
                if (!Service.RepeaterReady)
                    return;

                if (string.IsNullOrWhiteSpace(message))
                {
                    await ReplyErrorAsync(Strings.MessageEmpty(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                var startTimeOfDay = dt?.InputTimeUtc.TimeOfDay;
                var realInterval = interval?.Time ?? (startTimeOfDay is null
                    ? TimeSpan.FromMinutes(5)
                    : TimeSpan.FromDays(1));

                if (interval != null)
                {
                    if (interval.Time > TimeSpan.FromMinutes(25000))
                    {
                        await ReplyErrorAsync(Strings.IntervalTooLong(ctx.Guild.Id)).ConfigureAwait(false);
                        return;
                    }

                    if (interval.Time < TimeSpan.FromSeconds(5))
                    {
                        await ReplyErrorAsync(Strings.IntervalTooShort(ctx.Guild.Id)).ConfigureAwait(false);
                        return;
                    }
                }

                var runner = await Service.CreateRepeaterAsync(
                    ctx.Guild.Id,
                    ctx.Channel.Id,
                    realInterval,
                    message,
                    startTimeOfDay?.ToString(),
                    ((IGuildUser)ctx.User).GuildPermissions.MentionEveryone);

                if (runner == null)
                {
                    await ReplyErrorAsync(Strings.RepeatCreationFailed(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                var description = GetRepeaterInfoString(runner);
                await ctx.Channel.EmbedAsync(new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle(Strings.RepeaterCreated(ctx.Guild.Id))
                    .WithDescription(description)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating repeater");
                await ReplyErrorAsync(Strings.ErrorCreatingRepeater(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }
        /// <summary>
        ///     Lists all active repeaters in the guild.
        /// </summary>
        /// <remarks>
        ///     Shows index, channel, interval, and next execution time for each repeater.
        ///     Requires the Manage Messages permission.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task RepeatList()
        {
            if (!Service.RepeaterReady)
                return;

            var repeaters = Service.GetGuildRepeaters(ctx.Guild.Id);

            if (!repeaters.Any())
            {
                await ReplyErrorAsync(Strings.NoActiveRepeaters(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(repeaters.Count / 5)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
                .ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);

                var pageBuilder = new PageBuilder()
                    .WithOkColor()
                    .WithTitle(Strings.ListOfRepeaters(ctx.Guild.Id));

                var pageRepeaters = repeaters.Skip(page * 5).Take(5);
                var i = page * 5;

                foreach (var repeater in pageRepeaters)
                {
                    var description = GetRepeaterInfoString(repeater);
                    pageBuilder.AddField(
                        $"#{Format.Code((i + 1).ToString())}",
                        description
                    );
                    i++;
                }

                return pageBuilder;
            }
        }
        /// <summary>
        ///     Updates the message of an existing repeater.
        /// </summary>
        /// <param name="index">The one-based index of the repeater to update.</param>
        /// <param name="message">The new message for the repeater.</param>
        /// <remarks>
        ///     Only changes the message content, keeping all other settings the same.
        ///     Requires the Manage Messages permission.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task RepeatMessage(int index, [Remainder] string? message)
        {
            if (!Service.RepeaterReady)
                return;

            if (string.IsNullOrWhiteSpace(message))
            {
                await ReplyErrorAsync(Strings.MessageEmpty(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var repeater = Service.GetRepeaterByIndex(ctx.Guild.Id, index - 1);
            if (repeater == null)
            {
                await ReplyErrorAsync(Strings.IndexOutOfRange(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var success = await Service.UpdateRepeaterMessageAsync(
                ctx.Guild.Id,
                repeater.Repeater.Id,
                message,
                ((IGuildUser)ctx.User).GuildPermissions.MentionEveryone);

            if (!success)
            {
                await ReplyErrorAsync(Strings.RepeatActionFailed(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(Strings.RepeaterMsgUpdate(ctx.Guild.Id, message)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Changes the channel where a repeater sends its messages.
        /// </summary>
        /// <param name="index">The one-based index of the repeater to modify.</param>
        /// <param name="textChannel">The new channel for the repeater. Defaults to current channel if not specified.</param>
        /// <remarks>
        ///     The bot must have permission to send messages in the target channel.
        ///     Requires the Manage Messages permission.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task RepeatChannel(int index, [Remainder] ITextChannel? textChannel = null)
        {
            if (!Service.RepeaterReady)
                return;

            textChannel ??= ctx.Channel as ITextChannel;
            if (textChannel == null)
                return;

            var repeater = Service.GetRepeaterByIndex(ctx.Guild.Id, index - 1);
            if (repeater == null)
            {
                await ReplyErrorAsync(Strings.IndexOutOfRange(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var success = await Service.UpdateRepeaterChannelAsync(
                ctx.Guild.Id,
                repeater.Repeater.Id,
                textChannel.Id);

            if (!success)
            {
                await ReplyErrorAsync(Strings.RepeatActionFailed(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(Strings.RepeaterChannelUpdate(ctx.Guild.Id, textChannel.Mention))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Formats repeater information into a human-readable string.
        /// </summary>
        /// <param name="runner">The repeater runner to get information from.</param>
        /// <returns>A formatted string containing the repeater's details.</returns>
        private string GetRepeaterInfoString(RepeatRunner runner)
        {
            var intervalString = Format.Bold(TimeSpan.Parse(runner.Repeater.Interval).ToPrettyStringHm());
            var executesIn = runner.NextDateTime - DateTime.UtcNow;
            var executesInString = Format.Bold(executesIn.ToPrettyStringHm());
            var message = Format.Sanitize(runner.Repeater.Message.TrimTo(50));

            var description = "";
            if (runner.Repeater.NoRedundant)
                description = $"{Format.Underline(Format.Bold(Strings.NoRedundant(ctx.Guild.Id)))}\n\n";

            description +=
                $"<#{runner.Repeater.ChannelId}>\n`{Strings.Interval(ctx.Guild.Id)}` {intervalString}\n`{Strings.ExecutesIn(ctx.Guild.Id)}` {executesInString}\n`{Strings.Message(ctx.Guild.Id)}` {message}";

            return description;
        }
    }
}
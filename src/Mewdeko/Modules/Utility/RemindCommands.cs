using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Utility.Services;
using Swan;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    ///     Provides commands for managing reminders.
    /// </summary>
    [Group]
    public class RemindCommands(InteractiveService interactivity) : MewdekoSubmodule<RemindService>
    {
        /// <summary>
        ///     Determines whether the reminder should be sent to the user directly or to the channel.
        /// </summary>
        public enum MeOrHere
        {
            /// <summary>
            ///     Sends the reminder to the user directly.
            /// </summary>
            Me,

            /// <summary>
            ///     Sends the reminder to the channel.
            /// </summary>
            Here
        }

       /// <summary>
        /// Creates a reminder for the user or the current channel.
        /// </summary>
        /// <param name="meorhere">Specifies whether to send the reminder to the user or the channel.</param>
        /// <param name="remindString">The reminder message and timing information.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [Priority(1)]
        public async Task Remind(MeOrHere meorhere, [Remainder] string remindString)
        {
            if (!Service.TryParseRemindMessage(remindString, out var remindData))
            {
                await ReplyErrorAsync(Strings.RemindInvalidFormat(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var target = meorhere == MeOrHere.Me ? ctx.User.Id : ctx.Channel.Id;
            var isPrivate = meorhere == MeOrHere.Me || ctx.Guild == null;
            var shouldSanitize = ctx.Guild != null &&
                                 !((IGuildUser)ctx.User).GetPermissions((IGuildChannel)ctx.Channel).MentionEveryone;

            var (success, message) = await Service.CreateReminderAsync(
                target,
                isPrivate,
                remindData.Time,
                remindData.What,
                ctx.User.Id,
                ctx.Guild?.Id,
                shouldSanitize
            );

            if (success)
                await ctx.Channel.SendConfirmAsync(message).ConfigureAwait(false);
            else
                await ReplyErrorAsync(Strings.RemindTooLong(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a reminder for a specific text channel.
        /// </summary>
        /// <param name="channel">The text channel to send the reminder to.</param>
        /// <param name="remindString">The reminder message and timing information.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        [Priority(0)]
        public async Task Remind(ITextChannel channel, [Remainder] string remindString)
        {
            var perms = ((IGuildUser)ctx.User).GetPermissions(channel);
            if (!perms.SendMessages || !perms.ViewChannel)
            {
                await ReplyErrorAsync(Strings.CantReadOrSend(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (!Service.TryParseRemindMessage(remindString, out var remindData))
            {
                await ReplyErrorAsync(Strings.RemindInvalidFormat(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var shouldSanitize = !perms.MentionEveryone;
            var (success, message) = await Service.CreateReminderAsync(
                channel.Id,
                false,
                remindData.Time,
                remindData.What,
                ctx.User.Id,
                ctx.Guild.Id,
                shouldSanitize
            );

            if (success)
                await ctx.Channel.SendConfirmAsync(message).ConfigureAwait(false);
            else
                await ReplyErrorAsync(Strings.RemindTooLong(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists all reminders for the current user.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        public async Task RemindList()
        {
            var reminders = await Service.GetUserRemindersAsync(ctx.User.Id);

            if (reminders.Count==0)
            {
                await ReplyErrorAsync(Strings.RemindersNone(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(reminders.Count / 10)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
                .ConfigureAwait(false);
            return;

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);

                var pageBuilder = new PageBuilder()
                    .WithOkColor()
                    .WithTitle(Strings.ReminderList(ctx.Guild.Id));

                var pageReminders = reminders.Skip(page * 10).Take(10);
                var i = page * 10;

                foreach (var rem in pageReminders)
                {
                    var when = rem.When;
                    var diff = when - DateTime.UtcNow;
                    pageBuilder.AddField(
                        $"#{++i} {rem.When:HH:mm yyyy-MM-dd} UTC (in {(int)diff.TotalHours}h {diff.Minutes}m)",
                        $"""
                         `Target:` {(rem.IsPrivate ? "DM" : "Channel")}
                         `TargetId:` {rem.ChannelId}
                         `Message:` {rem.Message?.TrimTo(50)}
                         """);
                }

                return pageBuilder;
            }
        }

        /// <summary>
        /// Deletes a specific reminder.
        /// </summary>
        /// <param name="index">The index of the reminder to delete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        public async Task RemindDelete(int index)
        {
            if (--index < 0)
                return;

            var deleted = await Service.DeleteReminderAsync(ctx.User.Id, index);

            if (!deleted)
                await ReplyErrorAsync(Strings.ReminderNotExist(ctx.Guild.Id)).ConfigureAwait(false);
            else
                await ReplyErrorAsync(Strings.ReminderDeleted(ctx.Guild.Id, index + 1)).ConfigureAwait(false);
        }
    }
}
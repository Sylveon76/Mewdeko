using Discord.Interactions;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Utility.Services;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Swan;

namespace Mewdeko.Modules.Utility;

/// <summary>
///     Handles commands for setting, viewing, and managing reminders.
/// </summary>
[Group("remind", "remind")]
public class SlashRemindCommands(InteractiveService interactivity) : MewdekoSlashModuleBase<RemindService>
{
    /// <summary>
    ///     Sends a reminder to the user invoking the command.
    /// </summary>
    /// <param name="time">When the reminder should trigger.</param>
    /// <param name="reminder">The message for the reminder. If empty, prompts the user to input the reminder text.</param>
    /// <returns>A task that represents the asynchronous operation of adding a personal reminder.</returns>
    [SlashCommand("me", "Send a reminder to yourself.")]
    public async Task Me(
        [Summary("time", "When should the reminder respond.")] TimeSpan time,
        [Summary("reminder", "(optional) what should the reminder message be")] string? reminder = "")
    {
        await DeferAsync(true);
        if (string.IsNullOrEmpty(reminder))
        {
            await RespondWithModalAsync<ReminderModal>($"remind:{ctx.User.Id},1,{time};")
                .ConfigureAwait(false);
            return;
        }

        var (success, message) = await Service.CreateReminderAsync(
            ctx.User.Id,
            true,
            time,
            reminder,
            ctx.User.Id,
            ctx.Guild?.Id,
            false
        );

        if (success)
        {
            await ReplyConfirmAsync(message).ConfigureAwait(false);
        }
        else
        {
            await ReplyErrorAsync(Strings.RemindTooLong(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Sends a reminder to the channel where the command was invoked.
    /// </summary>
    /// <param name="time">When the reminder should trigger.</param>
    /// <param name="reminder">The message for the reminder. If empty, prompts the user to input the reminder text.</param>
    /// <returns>A task that represents the asynchronous operation of adding a channel reminder.</returns>
    [SlashCommand("here", "Send a reminder to this channel.")]
    public async Task Here(
        [Summary("time", "When should the reminder respond.")] TimeSpan time,
        [Summary("reminder", "(optional) what should the reminder message be")] string? reminder = "")
    {
        if (ctx.Guild is null)
        {
            await Me(time, reminder).ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrEmpty(reminder))
        {
            await RespondWithModalAsync<ReminderModal>($"remind:{ctx.Channel.Id},0,{time};")
                .ConfigureAwait(false);
            return;
        }

        var shouldSanitize = !((IGuildUser)ctx.User).GetPermissions((IGuildChannel)ctx.Channel).MentionEveryone;
        var (success, message) = await Service.CreateReminderAsync(
            ctx.Channel.Id,
            false,
            time,
            reminder,
            ctx.User.Id,
            ctx.Guild.Id,
            shouldSanitize
        );

        if (success)
        {
            await ReplyConfirmAsync(message).ConfigureAwait(false);
        }
        else
        {
            await ReplyErrorAsync(Strings.RemindTooLong(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Sends a reminder to a specified channel.
    /// </summary>
    /// <param name="channel">The target channel for the reminder.</param>
    /// <param name="time">When the reminder should trigger.</param>
    /// <param name="reminder">The message for the reminder. If empty, prompts the user to input the reminder text.</param>
    /// <returns>A task that represents the asynchronous operation of adding a reminder to a specific channel.</returns>
    [SlashCommand("channel", "Send a reminder to this channel.")]
    [UserPerm(ChannelPermission.ManageMessages)]
    public async Task Channel(
        [Summary("channel", "where should the reminder be sent?")] ITextChannel channel,
        [Summary("time", "When should the reminder respond.")] TimeSpan time,
        [Summary("reminder", "(optional) what should the reminder message be")] string? reminder = "")
    {
        var perms = ((IGuildUser)ctx.User).GetPermissions(channel);
        if (!perms.SendMessages || !perms.ViewChannel)
        {
            await ReplyErrorAsync(Strings.CantReadOrSend(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrEmpty(reminder))
        {
            await RespondWithModalAsync<ReminderModal>($"remind:{channel.Id},0,{time};")
                .ConfigureAwait(false);
            return;
        }

        var shouldSanitize = !perms.MentionEveryone;
        var (success, message) = await Service.CreateReminderAsync(
            channel.Id,
            false,
            time,
            reminder,
            ctx.User.Id,
            ctx.Guild.Id,
            shouldSanitize
        );

        if (success)
        {
            await ReplyConfirmAsync(message).ConfigureAwait(false);
        }
        else
        {
            await ReplyErrorAsync(Strings.RemindTooLong(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Handles the modal interaction for creating a reminder.
    /// </summary>
    /// <param name="sId">The target ID for the reminder, either a user or a channel.</param>
    /// <param name="sPri">Indicates if the reminder is private.</param>
    /// <param name="sTime">The time when the reminder should trigger.</param>
    /// <param name="modal">The modal containing the reminder text.</param>
    /// <returns>A task that represents the asynchronous operation of processing the reminder modal submission.</returns>
    [ModalInteraction("remind:*,*,*;", true)]
    public async Task ReminderModal(string sId, string sPri, string sTime, ReminderModal modal)
    {
        var id = ulong.Parse(sId);
        var pri = int.Parse(sPri) == 1;
        var time = TimeSpan.Parse(sTime);
        await DeferAsync(pri);

        var shouldSanitize = ctx.Guild != null &&
            !((IGuildUser)ctx.User).GetPermissions((IGuildChannel)ctx.Channel).MentionEveryone;

        var (success, message) = await Service.CreateReminderAsync(
            id,
            pri,
            time,
            modal.Reminder,
            ctx.User.Id,
            ctx.Guild?.Id,
            shouldSanitize
        );

        if (success)
        {
            await ReplyConfirmAsync(message).ConfigureAwait(false);
        }
        else
        {
            await ReplyErrorAsync(Strings.RemindTooLong(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Lists the current reminders set by the user.
    /// </summary>
    /// <param name="page">The page of reminders to display, starting at 1.</param>
    /// <returns>A task that represents the asynchronous operation of listing reminders.</returns>
    [SlashCommand("list", "List your current reminders")]
    public async Task List(
        [Summary("page", "What page of reminders do you want to load.")]
        int page = 1)
    {
        await ctx.Interaction.SendConfirmAsync(Strings.Loading(ctx.Guild.Id)).ConfigureAwait(false);

        var reminders = await Service.GetUserRemindersAsync(ctx.User.Id);

        if (!reminders.Any())
        {
            await ctx.Interaction.DeleteOriginalResponseAsync().ConfigureAwait(false);
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

        await ctx.Interaction.DeleteOriginalResponseAsync().ConfigureAwait(false);
        await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
            .ConfigureAwait(false);

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
    ///     Deletes a specific reminder.
    /// </summary>
    /// <param name="index">The index of the reminder to delete, as displayed in the reminder list.</param>
    /// <returns>A task that represents the asynchronous operation of deleting a reminder.</returns>
    [SlashCommand("delete", "Delete a reminder")]
    public async Task RemindDelete([Summary("index", "The reminders index (from /remind list)")] int index)
    {
        if (--index < 0)
            return;

        var success = await Service.DeleteReminderAsync(ctx.User.Id, index);

        if (!success)
            await ReplyErrorAsync(Strings.ReminderNotExist(ctx.Guild.Id)).ConfigureAwait(false);
        else
            await ReplyConfirmAsync(Strings.ReminderDeleted(ctx.Guild.Id, index + 1)).ConfigureAwait(false);
    }
}
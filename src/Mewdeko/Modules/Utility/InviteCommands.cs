using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    /// Provides commands for managing and viewing invite-related information.
    /// </summary>
    [Group]
    public class InviteCommands : MewdekoSubmodule<InviteCountService>
    {
        private readonly InteractiveService interactiveService;

        /// <summary>
        /// Initializes a new instance of the <see cref="InviteCommands"/> class.
        /// </summary>
        /// <param name="serv">The interactive service for handling paginated responses.</param>
        public InviteCommands(InteractiveService serv)
        {
            interactiveService = serv;
        }

        /// <summary>
        /// Displays the number of invites for a user.
        /// </summary>
        /// <param name="user">The user to check invites for. If null, checks for the command user.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Invites(IUser user = null)
        {
            user ??= Context.User;
            var invites = await Service.GetInviteCount(user.Id, Context.Guild.Id);
            await ReplyConfirmAsync(Strings.UserInviteCount(ctx.Guild.Id, user, invites));
        }

        /// <summary>
        /// Displays the current invite settings for the guild.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task InviteSettings()
        {
            var settings = await Service.GetInviteCountSettingsAsync(Context.Guild.Id);
            await ReplyConfirmAsync(Strings.InviteSettings(ctx.Guild.Id,
                GetEnDis(settings.IsEnabled),
                GetEnDis(settings.RemoveInviteOnLeave),
                    settings.MinAccountAge));
        }

        /// <summary>
        /// Toggles invite tracking for the guild.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task ToggleInviteTracking()
        {
            var newState = await Service.SetInviteTrackingEnabledAsync(Context.Guild.Id,
                !(await Service.GetInviteCountSettingsAsync(Context.Guild.Id)).IsEnabled);
            await ReplyConfirmAsync(newState
                ? Strings.InviteTrackingEnabled(Context.Guild.Id)
                : Strings.InviteTrackingDisabled(Context.Guild.Id));
        }

        /// <summary>
        /// Toggles whether invites should be removed when a user leaves the guild.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task ToggleRemoveInviteOnLeave()
        {
            var newState = await Service.SetRemoveInviteOnLeaveAsync(Context.Guild.Id,
                !(await Service.GetInviteCountSettingsAsync(Context.Guild.Id)).RemoveInviteOnLeave);
            await ReplyConfirmAsync(newState
                ? Strings.RemoveInviteOnLeaveEnabled(Context.Guild.Id)
                : Strings.RemoveInviteOnLeaveDisabled(Context.Guild.Id));
        }

        /// <summary>
        /// Sets the minimum account age required for an invite to be counted.
        /// </summary>
        /// <param name="days">The minimum age in days.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task SetMinAccountAge(int days)
        {
            var minAge = TimeSpan.FromDays(days);
            await Service.SetMinAccountAgeAsync(Context.Guild.Id, minAge);
            await ReplyConfirmAsync(Strings.MinAccountAgeSet(ctx.Guild.Id, days));
        }

        /// <summary>
        /// Displays a leaderboard of users with the most invites.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task InviteLeaderboard()
        {
            var leaderboard = await Service.GetInviteLeaderboardAsync(Context.Guild);

            if (leaderboard.Count == 0)
            {
                await ReplyErrorAsync(Strings.NoInviteData(ctx.Guild.Id));
                return;
            }

            var paginator = new LazyPaginatorBuilder()
                .AddUser(Context.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(leaderboard.Count / 20)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await interactiveService.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
                .ConfigureAwait(false);
            return;

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                return new PageBuilder().WithOkColor()
                    .WithTitle(Strings.InviteLeaderboardTitle(ctx.Guild.Id))
                    .WithDescription(string.Join("\n", leaderboard.Skip(page * 20).Take(20)
                        .Select((x, i) => $"{i + 1 + page * 20}. {x.Username} - {x.InviteCount} invites")));
            }
        }

        /// <summary>
        /// Displays who invited a specific user to the guild.
        /// </summary>
        /// <param name="user">The user to check. If null, checks for the command user.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task WhoInvited(IUser user = null)
        {
            user ??= Context.User;
            var inviter = await Service.GetInviter(user.Id, Context.Guild);

            if (inviter == null)
                await ReplyErrorAsync(Strings.NoInviterFound(ctx.Guild.Id, user.Username));
            else
                await ReplyConfirmAsync(Strings.InviterFound(ctx.Guild.Id, user.Username, inviter.Username));
        }

        /// <summary>
        /// Displays a list of users invited by a specific user.
        /// </summary>
        /// <param name="user">The user whose invites to check. If null, checks for the command user.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task InvitedUsers(IUser user = null)
        {
            user ??= Context.User;
            var invitedUsers = await Service.GetInvitedUsers(user.Id, Context.Guild);

            if (invitedUsers.Count == 0)
            {
                await ReplyErrorAsync(Strings.NoInvitedUsers(ctx.Guild.Id, user.Username));
                return;
            }

            var paginator = new LazyPaginatorBuilder()
                .AddUser(Context.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(invitedUsers.Count / 20)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await interactiveService.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
                .ConfigureAwait(false);
            return;

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                return new PageBuilder().WithOkColor()
                    .WithTitle(Strings.InvitedUsersTitle(ctx.Guild.Id, user.Username))
                    .WithDescription(string.Join("\n",
                        invitedUsers.Skip(page * 20).Take(20).Select(x => x.ToString())));
            }
        }

        private string GetEnDis(bool endis)
            => endis ? "Enabled" : "Disabled";
    }
}
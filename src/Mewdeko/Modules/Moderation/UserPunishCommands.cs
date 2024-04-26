using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Moderation.Services;
using Mewdeko.Services.Settings;
using NekosBestApiNet;
using Serilog;
using Swan;
using UserExtensions = Mewdeko.Extensions.UserExtensions;

namespace Mewdeko.Modules.Moderation;

public partial class Moderation : MewdekoModule
{
    /// <summary>
    /// Module for user punishment commands.
    /// </summary>
    /// <param name="mute">The mute service</param>
    /// <param name="db">The database service</param>
    /// <param name="serv">The service used to handle embed pagination</param>
    /// <param name="nekos">The NekosBest API</param>
    /// <param name="config">The bot config service used for yml based configs</param>
    [Group]
    public class UserPunishCommands(
        MuteService mute,
        DbService db,
        InteractiveService serv,
        NekosBestApi nekos,
        BotConfigService config)
        : MewdekoSubmodule<UserPunishService>
    {
        /// <summary>
        /// The thing used for addrole in commands to be able to parse it
        /// </summary>
        public enum AddRole
        {
            /// <summary>
            /// Add role
            /// </summary>
            AddRole
        }


        /// <summary>
        /// Gets progress for a mass nickname operation.
        /// </summary>
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task MassNickProgress()
        {
            var massNick = Service.GetMassNick(ctx.Guild.Id);
            if (massNick is null)
            {
                await ctx.Channel.SendErrorAsync("There is no mass nick in progress.", Config);
            }
            else
            {
                var eb = new EmbedBuilder()
                    .WithTitle($"{massNick.OperationType} Progress")
                    .WithDescription($"`Started By:` {massNick.StartedBy.Mention} | {massNick.StartedBy.Id}" +
                                     $"\n`Total:` {massNick.Total}" +
                                     $"\n`Changed:` {massNick.Changed}" +
                                     $"\n`Failed:` {massNick.Failed}" +
                                     $"\n`Started At:` {TimestampTag.FromDateTime(massNick.StartedAt)}");

                await ctx.Channel.SendMessageAsync(embed: eb.Build());
            }
        }

        /// <summary>
        /// Stops a mass nickname operation.
        /// </summary>
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task MassNickStop()
        {
            var massNick = Service.GetMassNick(ctx.Guild.Id);
            if (massNick is null)
            {
                await ctx.Channel.SendErrorAsync("There is no mass nick in progress.", Config);
            }
            else
            {
                Service.UpdateMassNick(ctx.Guild.Id, false, false, true);
                await ctx.Channel.SendConfirmAsync($"{config.Data.SuccessEmote} Stopping mass nick.");
            }
        }

        /// <summary>
        /// Dehoists all users in the guild by replacing special characters in their nicknames.
        /// </summary>
        /// <param name="onlyDehoistNicks">Whether to only dehoist nicknames</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageNicknames),
         BotPerm(GuildPermission.Administrator)]
        public async Task DehoistAll(bool onlyDehoistNicks = false)
        {
            var dehoistedUsers = new Dictionary<IGuildUser, string>();
            var users = (await ctx.Guild.GetUsersAsync()).ToList();
            Parallel.ForEach(users, user =>
            {
                var newNickname = onlyDehoistNicks
                    ? UserExtensions.ReplaceSpecialChars(user.Nickname)
                    : UserExtensions.ReplaceSpecialChars(user.Username);
                if (onlyDehoistNicks)
                {
                    if (newNickname != user.Nickname)
                        dehoistedUsers.Add(user, newNickname);
                }
                else
                {
                    if (newNickname != user.Username && user.Nickname != newNickname)
                        dehoistedUsers.Add(user, newNickname);
                }
            });

            if (!await PromptUserConfirmAsync($"Are you sure you want to sanitize {dehoistedUsers.Count} users?",
                    ctx.User.Id))
                return;

            if (!dehoistedUsers.Any())
            {
                await ctx.Channel.SendErrorAsync("There are no users to dehoist.", Config);
                return;
            }

            if (Service.AddMassNick(ctx.Guild.Id, ctx.User, dehoistedUsers.Count, "Dehoist", out var massNick))
            {
                await ctx.Channel.SendConfirmAsync(
                    $"{config.Data.LoadingEmote} Dehoisting {dehoistedUsers.Count} users. This may take a while.");
                foreach (var user in dehoistedUsers)
                {
                    massNick = Service.GetMassNick(ctx.Guild.Id);
                    if (massNick.Stopped)
                        continue;
                    try
                    {
                        await user.Key.ModifyAsync(u => u.Nickname = user.Value);
                        Service.UpdateMassNick(ctx.Guild.Id, false, true);
                    }
                    catch
                    {
                        Service.UpdateMassNick(ctx.Guild.Id, true, false);
                    }
                }

                if (massNick.Stopped)
                {
                    await ctx.Channel.SendConfirmAsync(
                        $"{config.Data.ErrorEmote} Dehoisting operation stopped.\nDehoisted {massNick.Changed} users.\nFailed to dehoist {massNick.Failed} users.");
                    Service.RemoveMassNick(ctx.Guild.Id);
                }
                else
                {
                    await ctx.Channel.SendConfirmAsync(
                        $"{config.Data.SuccessEmote} Dehoisting operation completed.\n Dehoisted {massNick.Changed} users.\nFailed to dehoist {massNick.Failed} users.");
                    Service.RemoveMassNick(ctx.Guild.Id);
                }
            }
            else
            {
                await ctx.Channel.SendConfirmAsync(
                    $"{config.Data.ErrorEmote} There is already a mass nickname operation running.");
            }
        }

        /// <summary>
        /// Sanitizes all users in the guild by replacing special characters in their usernames.
        /// </summary>
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageNicknames),
         BotPerm(GuildPermission.Administrator)]
        public async Task SanitizeAll()
        {
            var sanitizedUsers = new ConcurrentDictionary<IGuildUser, string>();
            var users = (await ctx.Guild.GetUsersAsync()).ToList();
            Parallel.ForEach(users, user =>
            {
                var newNickname = user.SanitizeUserName();
                if (newNickname != user.Username && user.Nickname != newNickname)
                    sanitizedUsers.TryAdd(user, newNickname);
            });
            if (!await PromptUserConfirmAsync($"Are you sure you want to sanitize {sanitizedUsers.Count} users?",
                    ctx.User.Id))
                return;
            if (!sanitizedUsers.Any())
            {
                await ctx.Channel.SendErrorAsync("There are no users to sanitize.", Config);
                return;
            }

            if (Service.AddMassNick(ctx.Guild.Id, ctx.User, sanitizedUsers.Count, "Sanitize", out var massNick))
            {
                await ctx.Channel.SendConfirmAsync(
                    $"{config.Data.LoadingEmote} Sanitizing {sanitizedUsers.Count} users. This may take a while.");
                foreach (var user in sanitizedUsers)
                {
                    massNick = Service.GetMassNick(ctx.Guild.Id);
                    if (massNick.Stopped)
                        continue;
                    try
                    {
                        await user.Key.ModifyAsync(u => u.Nickname = user.Value);
                        Service.UpdateMassNick(ctx.Guild.Id, false, true);
                    }
                    catch
                    {
                        Service.UpdateMassNick(ctx.Guild.Id, true, false);
                    }
                }

                if (massNick.Stopped)
                {
                    await ctx.Channel.SendConfirmAsync(
                        $"{config.Data.ErrorEmote} Sanitizing operation stopped.\nSanitized {massNick.Changed} users.\nFailed to santize {massNick.Failed} users.");
                    Service.RemoveMassNick(ctx.Guild.Id);
                }
                else
                {
                    await ctx.Channel.SendConfirmAsync(
                        $"{config.Data.SuccessEmote} Sanitizing operation completed.\nSanitized {massNick.Changed} users.\nFailed to sanitize {massNick.Failed} users.");
                    Service.RemoveMassNick(ctx.Guild.Id);
                }
            }
            else
            {
                await ctx.Channel.SendConfirmAsync(
                    $"{config.Data.ErrorEmote} There is already a mass nickname operation running.");
            }
        }

        /// <summary>
        /// Dehoists a user by replacing special characters in their username.
        /// </summary>
        /// <param name="user">The user to dehoist</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageNicknames),
         BotPerm(GuildPermission.ManageNicknames)]
        public async Task Dehoist(IGuildUser user)
        {
            if (user.Nickname != null && user.Nickname[0] < 'A')
            {
                var newNickname = UserExtensions.ReplaceSpecialChars(user.Nickname);
                await user.ModifyAsync(u => u.Nickname = newNickname);
                await ctx.Channel.SendConfirmAsync(
                    $"User {user.Mention} has been dehoisted. Their new nickname is: `{newNickname}`");
            }
            else
            {
                var newNickname = UserExtensions.ReplaceSpecialChars(user.Username);
                await user.ModifyAsync(u => u.Nickname = newNickname);
                await ctx.Channel.SendConfirmAsync(
                    $"{config.Data.SuccessEmote} User {user.Mention} has been dehoisted. Their new nickname is: `{newNickname}`");
            }
        }

        /// <summary>
        /// Sanitizes a user by replacing special characters in their username.
        /// </summary>
        /// <param name="user">The user to sanitize</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageNicknames),
         BotPerm(GuildPermission.ManageNicknames)]
        public async Task Sanitize(IGuildUser user)
        {
            var newName = user.SanitizeUserName();
            await user.ModifyAsync(u => u.Nickname = newName);
            await ctx.Channel.SendConfirmAsync(
                $"{config.Data.SuccessEmote} User {user.Mention} has been sanitized. Their new nickname is: `{newName}`");
        }

        /// <summary>
        /// Sets the warnlog channel for the guild.
        /// </summary>
        /// <param name="channel"></param>
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator), Priority(0)]
        public async Task SetWarnChannel([Remainder] ITextChannel channel)
        {
            var warnlogChannel = await Service.GetWarnlogChannel(ctx.Guild.Id);
            if (warnlogChannel == channel.Id)
            {
                await ctx.Channel.SendErrorAsync("This is already your warnlog channel!", Config).ConfigureAwait(false);
                return;
            }

            if (warnlogChannel == 0)
            {
                await Service.SetWarnlogChannelId(ctx.Guild, channel).ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync($"Your warnlog channel has been set to {channel.Mention}")
                    .ConfigureAwait(false);
                return;
            }

            var oldWarnChannel = await ctx.Guild.GetTextChannelAsync(warnlogChannel).ConfigureAwait(false);
            await Service.SetWarnlogChannelId(ctx.Guild, channel).ConfigureAwait(false);
            await ctx.Channel
                .SendConfirmAsync(
                    $"Your warnlog channel has been changed from {oldWarnChannel.Mention} to {channel.Mention}")
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Timeouts a user for a specified amount of time.
        /// </summary>
        /// <param name="time">The time to timeout the user</param>
        /// <param name="user">The user to timeout</param>
        /// <param name="reason">The reason for the timeout</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ModerateMembers),
         BotPerm(GuildPermission.ModerateMembers)]
        public async Task Timeout(StoopidTime time, IGuildUser user, [Remainder] string? reason = null)
        {
            if (!await CheckRoleHierarchy(user))
                return;
            reason ??= $"{ctx.User} || None Specified";
            if (time.Time.Days > 28)
            {
                await ReplyErrorLocalizedAsync("timeout_length_too_long").ConfigureAwait(false);
                return;
            }

            await user.SetTimeOutAsync(time.Time, new RequestOptions
            {
                AuditLogReason = $"{ctx.User} | {reason}"
            }).ConfigureAwait(false);
            await ReplyConfirmLocalizedAsync("timeout_set", user.Mention, time.Time.Humanize(maxUnit: TimeUnit.Day))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Removes a timeout from a user.
        /// </summary>
        /// <param name="user">The user to remove the timeout from</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ModerateMembers),
         BotPerm(GuildPermission.ModerateMembers)]
        public async Task UnTimeOut(IGuildUser user)
        {
            if (!await CheckRoleHierarchy(user))
                return;
            await user.RemoveTimeOutAsync(new RequestOptions
            {
                AuditLogReason = $"Removal requested by {ctx.User}"
            }).ConfigureAwait(false);
            await ReplyConfirmLocalizedAsync("timeout_removed", user.Mention).ConfigureAwait(false);
        }

        /// <summary>
        /// Warns a user with an optional reason.
        /// </summary>
        /// <param name="user">The user to warn</param>
        /// <param name="reason">The reason for the warn</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers)]
        public async Task Warn(IGuildUser user, [Remainder] string? reason = null)
        {
            if (!await CheckRoleHierarchy(user))
                return;

            var dmFailed = false;
            try
            {
                await (await user.CreateDMChannelAsync().ConfigureAwait(false)).EmbedAsync(new EmbedBuilder()
                        .WithErrorColor()
                        .WithDescription(GetText("warned_on", ctx.Guild.ToString()))
                        .AddField(efb => efb.WithName(GetText("moderator")).WithValue(ctx.User.ToString()))
                        .AddField(efb => efb.WithName(GetText("reason")).WithValue(reason ?? "-")))
                    .ConfigureAwait(false);
            }
            catch
            {
                dmFailed = true;
            }

            WarningPunishment punishment;
            try
            {
                punishment = await Service.Warn(ctx.Guild, user.Id, ctx.User, reason).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex.Message);
                var errorEmbed = new EmbedBuilder()
                    .WithErrorColor()
                    .WithDescription(GetText("cant_apply_punishment"));

                if (dmFailed) errorEmbed.WithFooter($"⚠️ {GetText("unable_to_dm_user")}");

                await ctx.Channel.EmbedAsync(errorEmbed);
                return;
            }

            var embed = new EmbedBuilder()
                .WithOkColor();
            if (punishment is null || punishment.Id is 0)
            {
                embed.WithDescription(GetText("user_warned",
                    Format.Bold(user.ToString())));
            }
            else
            {
                embed.WithDescription(GetText("user_warned_and_punished", Format.Bold(user.ToString()),
                    Format.Bold(punishment.Punishment.ToString())));
            }

            if (dmFailed) embed.WithFooter($"⚠️ {GetText("unable_to_dm_user")}");

            if (dmFailed) embed.WithFooter($"⚠️ {GetText("unable_to_dm_user")}");

            await ctx.Channel.EmbedAsync(embed);
            if (await Service.GetWarnlogChannel(ctx.Guild.Id) != 0)
            {
                var uow = db.GetDbContext();
                var warnings = uow.Warnings
                    .ForId(ctx.Guild.Id, user.Id)
                    .Count(w => !w.Forgiven && w.UserId == user.Id);
                var condition = punishment != null;
                var punishtime = condition ? TimeSpan.FromMinutes(punishment.Time).ToString() : " ";
                var punishaction = condition ? punishment.Punishment.Humanize() : "None";
                var channel = await ctx.Guild.GetTextChannelAsync(await Service.GetWarnlogChannel(ctx.Guild.Id));
                await channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                    .WithThumbnailUrl(user.RealAvatarUrl().ToString())
                    .WithTitle($"Warned by: {ctx.User}")
                    .WithCurrentTimestamp()
                    .WithDescription(
                        $"Username: {user.Username}#{user.Discriminator}\nID of Warned User: {user.Id}\nWarn Number: {warnings}\nPunishment: {punishaction} {punishtime}\n\nReason: {reason}\n\n[Click Here For Context](https://discord.com/channels/{ctx.Message.GetJumpUrl()})"));
            }
        }

        /// <summary>
        /// Sets the expiration time for warnings.
        /// </summary>
        /// <param name="days">The number of days until warnings expire</param>
        /// <param name="options">The action to take when a warning expires</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator), Priority(2)]
        public async Task WarnExpire(int days, WarnExpireAction options)
        {
            if (days is < 0 or > 366)
                return;

            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);

            await Service.WarnExpireAsync(ctx.Guild.Id, days, options).ConfigureAwait(false);
            if (days == 0)
            {
                await ReplyConfirmLocalizedAsync("warn_expire_reset").ConfigureAwait(false);
                return;
            }

            if (options == WarnExpireAction.Delete)
            {
                await ReplyConfirmLocalizedAsync("warn_expire_set_delete", Format.Bold(days.ToString()))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmLocalizedAsync("warn_expire_set_clear", Format.Bold(days.ToString()))
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Gets the warnlog for a user.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild), Priority(3), UserPerm(GuildPermission.BanMembers)]
        public Task Warnlog(IGuildUser user) => InternalWarnlog(user.Id);

        /// <summary>
        /// Gets the warnlog for the user who invoked the command.
        /// </summary>
        /// <returns></returns>
        public Task Warnlog() => InternalWarnlog(ctx.User.Id);

        /// <summary>
        /// Gets the warnlog for a user by their ID.
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers), Priority(1)]
        public Task Warnlog(ulong userId) => InternalWarnlog(userId);

        private async Task InternalWarnlog(ulong userId)
        {
            var warnings = Service.UserWarnings(ctx.Guild.Id, userId);
            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(warnings.Length / 9)
                .WithDefaultCanceledPage()
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();
            await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                warnings = warnings.Skip(page)
                    .Take(9)
                    .ToArray();

                var embed = new PageBuilder().WithOkColor()
                    .WithTitle(GetText("warnlog_for",
                        (ctx.Guild as SocketGuild)?.GetUser(userId)?.ToString() ?? userId.ToString()))
                    .WithFooter(efb => efb.WithText(GetText("page", page + 1)));

                if (warnings.Length == 0)
                {
                    embed.WithDescription(GetText("warnings_none"));
                }
                else
                {
                    var i = page * 9;
                    foreach (var w in warnings)
                    {
                        i++;
                        var name = GetText("warned_on_by", $"<t:{w.DateAdded.Value.ToUnixEpochDate()}:D>",
                            $"<t:{w.DateAdded.Value.ToUnixEpochDate()}:T>", w.Moderator);
                        if (w.Forgiven)
                            name = $"{Format.Strikethrough(name)} {GetText("warn_cleared_by", w.ForgivenBy)}";

                        embed.AddField(x => x
                            .WithName($"#`{i}` {name}")
                            .WithValue(w.Reason.TrimTo(1020)));
                    }
                }

                return embed;
            }
        }

        /// <summary>
        /// Gets how many warnings each user has.
        /// </summary>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers)]
        public async Task WarnlogAll()
        {
            var warnings = await Service.WarnlogAll(ctx.Guild.Id);

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(warnings.Length / 15)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                {
                    var ws = await warnings.Skip(page * 15)
                        .Take(15)
                        .ToArray()
                        .Select(async x =>
                        {
                            var all = x.Count();
                            var forgiven = x.Count(y => y.Forgiven);
                            var total = all - forgiven;
                            var usr = await ctx.Guild.GetUserAsync(x.Key).ConfigureAwait(false);
                            return $"{usr?.ToString() ?? x.Key.ToString()} | {total} ({all} - {forgiven})";
                        }).GetResults().ConfigureAwait(false);

                    return new PageBuilder().WithOkColor()
                        .WithTitle(GetText("warnings_list"))
                        .WithDescription(string.Join("\n", ws));
                }
            }
        }

        /// <summary>
        /// Clears a user's warnings. If an index is provided, clears only that warning.
        /// </summary>
        /// <param name="user">The user to clear warnings for</param>
        /// <param name="index">The index of the warning to clear</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers)]
        public async Task Warnclear(IGuildUser user, int index = 0)
        {
            if (index < 0)
                return;
            if (!await CheckRoleHierarchy(user))
                return;
            var success = await Service.WarnClearAsync(ctx.Guild.Id, user.Id, index, ctx.User.ToString())
                .ConfigureAwait(false);
            var userStr = user.ToString();
            if (index == 0)
            {
                await ReplyConfirmLocalizedAsync("warnings_cleared", userStr).ConfigureAwait(false);
            }
            else
            {
                if (success)
                {
                    await ReplyConfirmLocalizedAsync("warning_cleared", Format.Bold(index.ToString()), userStr)
                        .ConfigureAwait(false);
                }
                else
                {
                    await ReplyErrorLocalizedAsync("warning_clear_fail").ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Sets a punishment for a certain number of warnings.
        /// </summary>
        /// <param name="number">The number of warnings</param>
        /// <param name="_">The addrole keyword</param>
        /// <param name="role">The role to add</param>
        /// <param name="time">The time for the punishment</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers), Priority(1)]
        public async Task WarnPunish(int number, AddRole _, IRole role, StoopidTime? time = null)
        {
            const PunishmentAction punish = PunishmentAction.AddRole;
            var success = await Service.WarnPunish(ctx.Guild.Id, number, punish, time, role);

            if (!success)
                return;

            if (time is null)
            {
                await ReplyConfirmLocalizedAsync("warn_punish_set",
                    Format.Bold(punish.ToString()),
                    Format.Bold(number.ToString())).ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmLocalizedAsync("warn_punish_set_timed",
                    Format.Bold(punish.ToString()),
                    Format.Bold(number.ToString()),
                    Format.Bold(time.Input)).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Sets a punishment for a certain number of warnings.
        /// </summary>
        /// <param name="number">The number of warnings</param>
        /// <param name="punish">The punishment to apply</param>
        /// <param name="time">The time for the punishment</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers)]
        public async Task WarnPunish(int number, PunishmentAction punish, StoopidTime? time = null)
        {
            switch (punish)
            {
                // this should never happen. Addrole has its own method with higher priority
                case PunishmentAction.AddRole:
                case PunishmentAction.Warn:
                    return;
            }

            var success = await Service.WarnPunish(ctx.Guild.Id, number, punish, time);

            if (!success)
                return;
            switch (punish)
            {
                case PunishmentAction.Timeout when time?.Time.Days > 28:
                    await ReplyErrorLocalizedAsync("timeout_length_too_long").ConfigureAwait(false);
                    return;
                case PunishmentAction.Timeout when time is null:
                    await ReplyErrorLocalizedAsync("timeout_needs_time").ConfigureAwait(false);
                    return;
            }

            if (time is null)
            {
                await ReplyConfirmLocalizedAsync("warn_punish_set",
                    Format.Bold(punish.ToString()),
                    Format.Bold(number.ToString())).ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmLocalizedAsync("warn_punish_set_timed",
                    Format.Bold(punish.ToString()),
                    Format.Bold(number.ToString()),
                    Format.Bold(time.Input)).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Removes a punishment for a certain number of warnings.
        /// </summary>
        /// <param name="number">The number of warnings</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers)]
        public async Task WarnPunish(int number)
        {
            if (!await Service.WarnPunishRemove(ctx.Guild.Id, number)) return;

            await ReplyConfirmLocalizedAsync("warn_punish_rem",
                Format.Bold(number.ToString())).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists the warn punishments for the guild.
        /// </summary>
        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task WarnPunishList()
        {
            var ps = await Service.WarnPunishList(ctx.Guild.Id);

            string? list;
            if (ps.Length > 0)
            {
                list = string.Join("\n",
                    ps.Select(x =>
                        $"{x.Count} -> {x.Punishment} {(x.Punishment == PunishmentAction.AddRole ? $"<@&{x.RoleId}>" : "")} {(x.Time <= 0 ? "" : $"{x.Time}m")} "));
            }
            else
            {
                list = GetText("warnpl_none");
            }

            await ctx.Channel.SendConfirmAsync(
                GetText("warn_punish_list"),
                list).ConfigureAwait(false);
        }


        /// <summary>
        /// Bans a user for a specified amount of time.
        /// </summary>
        /// <param name="pruneTime">The amount of time to prune messages</param>
        /// <param name="time">The amount of time to ban the user</param>
        /// <param name="user">The user to ban</param>
        /// <param name="msg">The reason for the ban</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers), BotPerm(GuildPermission.BanMembers), Priority(1)]
        public async Task Ban(StoopidTime pruneTime, StoopidTime time, IGuildUser user, [Remainder] string? msg = null)
        {
            if (time.Time > TimeSpan.FromDays(49))
                return;

            if (pruneTime.Time > TimeSpan.FromDays(49))
                return;


            if (user != null && !await CheckRoleHierarchy(user).ConfigureAwait(false))
                return;

            var dmFailed = false;

            if (user != null)
            {
                try
                {
                    var defaultMessage = GetText("bandm", Format.Bold(ctx.Guild.Name), msg);
                    var (embedBuilder, message, components) = await Service
                        .GetBanUserDmEmbed(Context, user, defaultMessage, msg, time.Time).ConfigureAwait(false);
                    if (embedBuilder is not null || message is not null)
                    {
                        var userChannel = await user.CreateDMChannelAsync().ConfigureAwait(false);
                        await userChannel
                            .SendMessageAsync(message, embeds: embedBuilder, components: components?.Build())
                            .ConfigureAwait(false);
                    }
                }
                catch
                {
                    dmFailed = true;
                }
            }

            await mute.TimedBan(Context.Guild, user, time.Time, $"{ctx.User} | {msg}", pruneTime.Time)
                .ConfigureAwait(false);
            var toSend = new EmbedBuilder().WithOkColor()
                .WithTitle($"⛔️ {GetText("banned_user")}")
                .AddField(efb => efb.WithName(GetText("username")).WithValue(user.ToString()).WithIsInline(true))
                .AddField(efb => efb.WithName("ID").WithValue(user.Id.ToString()).WithIsInline(true))
                .AddField(efb =>
                    efb.WithName(GetText("duration"))
                        .WithValue($"{time.Time.Days}d {time.Time.Hours}h {time.Time.Minutes}m")
                        .WithIsInline(true));

            if (dmFailed) toSend.WithFooter($"⚠️ {GetText("unable_to_dm_user")}");

            await ctx.Channel.EmbedAsync(toSend)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Bans a user.
        /// </summary>
        /// <param name="user">The user to ban</param>
        /// <param name="msg">The reason for the ban</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers), BotPerm(GuildPermission.BanMembers), Priority(0)]
        public async Task Ban(IUser? user, [Remainder] string? msg = null)
        {
            if (user is null)
            {
                await ctx.Guild.AddBanAsync(user, 7, options: new RequestOptions
                {
                    AuditLogReason = $"{ctx.User} | {msg}"
                }).ConfigureAwait(false);

                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithTitle($"⛔️ {GetText("banned_user")}")
                        .AddField(efb => efb.WithName("ID").WithValue(user.Id).WithIsInline(true)))
                    .ConfigureAwait(false);
            }
            else
            {
                await Ban(user, msg).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Bans a user for a specified amount of time.
        /// </summary>
        /// <param name="time">The amount of time to prune messages</param>
        /// <param name="userId">The ID of the user to ban</param>
        /// <param name="msg">The reason for the ban</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers), BotPerm(GuildPermission.BanMembers), Priority(0)]
        public async Task Ban(StoopidTime time, IUser userId, [Remainder] string? msg = null)
        {
            await ctx.Guild.AddBanAsync(userId, time.Time.Days, options: new RequestOptions
            {
                AuditLogReason = $"{ctx.User} | {msg}"
            }).ConfigureAwait(false);

            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithTitle($"⛔️ {GetText("banned_user")}")
                    .AddField(efb => efb.WithName("ID").WithValue(userId.ToString()).WithIsInline(true)))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Bans a user.
        /// </summary>
        /// <param name="userId">The ID of the user to ban</param>
        /// <param name="msg">The reason for the ban</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers), BotPerm(GuildPermission.BanMembers), Priority(2)]
        public async Task Ban(IGuildUser user, [Remainder] string? msg = null)
        {
            if (!await CheckRoleHierarchy(user).ConfigureAwait(false))
                return;

            var dmFailed = false;

            try
            {
                var defaultMessage = GetText("bandm", Format.Bold(ctx.Guild.Name), msg);
                var (embedBuilder, message, components) = await Service
                    .GetBanUserDmEmbed(Context, user, defaultMessage, msg, null).ConfigureAwait(false);
                if (embedBuilder is not null || message is not null)
                {
                    var userChannel = await user.CreateDMChannelAsync().ConfigureAwait(false);
                    await userChannel.SendMessageAsync(message, embeds: embedBuilder, components: components?.Build())
                        .ConfigureAwait(false);
                }
            }
            catch
            {
                dmFailed = true;
            }

            await ctx.Guild.AddBanAsync(user, 7, options: new RequestOptions
            {
                AuditLogReason = $"{ctx.User} | {msg}"
            }).ConfigureAwait(false);

            var toSend = new EmbedBuilder().WithOkColor()
                .WithTitle($"⛔️ {GetText("banned_user")}")
                .AddField(efb => efb.WithName(GetText("username")).WithValue(user.ToString()).WithIsInline(true))
                .AddField(efb => efb.WithName("ID").WithValue(user.Id.ToString()).WithIsInline(true))
                .WithImageUrl((await nekos.ActionsApi.Kick().ConfigureAwait(false)).Results.First().Url);

            if (dmFailed) toSend.WithFooter($"⚠️ {GetText("unable_to_dm_user")}");

            await ctx.Channel.EmbedAsync(toSend)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Bans a user with the specified reason and amount of days to purge messages.
        /// </summary>
        /// <param name="time">The amount of time to prune messages</param>
        /// <param name="user">The user to ban</param>
        /// <param name="msg">The reason for the ban</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers), BotPerm(GuildPermission.BanMembers), Priority(2)]
        public async Task Ban(StoopidTime time, IGuildUser user, [Remainder] string? msg = null)
        {
            if (!await CheckRoleHierarchy(user).ConfigureAwait(false))
                return;

            var dmFailed = false;

            try
            {
                var defaultMessage = GetText("bandm", Format.Bold(ctx.Guild.Name), msg);
                var (embedBuilder, message, components) = await Service
                    .GetBanUserDmEmbed(Context, user, defaultMessage, msg, null).ConfigureAwait(false);
                if (embedBuilder is not null || message is not null)
                {
                    var userChannel = await user.CreateDMChannelAsync().ConfigureAwait(false);
                    await userChannel.SendMessageAsync(message, embeds: embedBuilder, components: components?.Build())
                        .ConfigureAwait(false);
                }
            }
            catch
            {
                dmFailed = true;
            }

            await ctx.Guild.AddBanAsync(user, time.Time.Days, options: new RequestOptions
            {
                AuditLogReason = $"{ctx.User} | {msg}"
            }).ConfigureAwait(false);

            var toSend = new EmbedBuilder().WithOkColor()
                .WithTitle($"⛔️ {GetText("banned_user")}")
                .AddField(efb => efb.WithName(GetText("username")).WithValue(user.ToString()).WithIsInline(true))
                .AddField(efb => efb.WithName("ID").WithValue(user.Id.ToString()).WithIsInline(true))
                .WithImageUrl((await nekos.ActionsApi.Kick().ConfigureAwait(false)).Results.First().Url);

            if (dmFailed) toSend.WithFooter($"⚠️ {GetText("unable_to_dm_user")}");

            await ctx.Channel.EmbedAsync(toSend)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Sets the message that users get dmed with when they are banned.
        /// </summary>
        /// <param name="message">The message to set, can also be embedcode</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers), BotPerm(GuildPermission.BanMembers)]
        public async Task BanMessage([Remainder] string? message = null)
        {
            if (message is null)
            {
                var template = Service.GetBanTemplate(Context.Guild.Id);
                if (template is null)
                {
                    await ReplyConfirmLocalizedAsync("banmsg_default").ConfigureAwait(false);
                    return;
                }

                await Context.Channel.SendConfirmAsync(template).ConfigureAwait(false);
                return;
            }

            Service.SetBanTemplate(Context.Guild.Id, message);
            await ctx.OkAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Resets the ban message to the default message.
        /// </summary>
        /// <returns></returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers), BotPerm(GuildPermission.BanMembers)]
        public Task BanMsgReset()
        {
            Service.SetBanTemplate(Context.Guild.Id, null);
            return ctx.OkAsync();
        }

        /// <summary>
        /// Tests the ban message. Use it as a prank!
        /// </summary>
        /// <param name="reason">The reason for the ban</param>
        /// <returns></returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers), BotPerm(GuildPermission.BanMembers), Priority(0)]
        public Task BanMessageTest([Remainder] string? reason = null) => InternalBanMessageTest(reason, null);

        /// <summary>
        /// Tests the ban message. Use it as a prank!
        /// </summary>
        /// <param name="duration">The duration of the ban</param>
        /// <param name="reason">The reason for the ban</param>
        /// <returns></returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers), BotPerm(GuildPermission.BanMembers), Priority(1)]
        public Task BanMessageTest(StoopidTime duration, [Remainder] string? reason = null) =>
            InternalBanMessageTest(reason, duration.Time);

        private async Task InternalBanMessageTest(string? reason, TimeSpan? duration)
        {
            var dmChannel = await ctx.User.CreateDMChannelAsync().ConfigureAwait(false);
            var defaultMessage = GetText("bandm", Format.Bold(ctx.Guild.Name), reason);
            var crEmbed = await Service.GetBanUserDmEmbed(Context,
                (IGuildUser)Context.User,
                defaultMessage,
                reason,
                duration).ConfigureAwait(false);

            if (crEmbed.Item1 is null && crEmbed.Item2 is null)
            {
                await ConfirmLocalizedAsync("bandm_disabled").ConfigureAwait(false);
            }
            else
            {
                try
                {
                    await dmChannel
                        .SendMessageAsync(crEmbed.Item2, embeds: crEmbed.Item1, components: crEmbed.Item3?.Build())
                        .ConfigureAwait(false);
                }
                catch (Exception)
                {
                    await ReplyErrorLocalizedAsync("unable_to_dm_user").ConfigureAwait(false);
                    return;
                }

                await Context.OkAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Unbans a user.
        /// </summary>
        /// <param name="user">The user to unban</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers), BotPerm(GuildPermission.BanMembers)]
        public async Task Unban([Remainder] string user)
        {
            var bans = await ctx.Guild.GetBansAsync().FlattenAsync().ConfigureAwait(false);

            var bun = bans.FirstOrDefault(x =>
                string.Equals(x.User.ToString(), user, StringComparison.InvariantCultureIgnoreCase));

            if (bun == null)
            {
                await ReplyErrorLocalizedAsync("user_not_found").ConfigureAwait(false);
                return;
            }

            await UnbanInternal(bun.User).ConfigureAwait(false);
        }

        /// <summary>
        /// Unbans a user.
        /// </summary>
        /// <param name="userId">The ID of the user to unban</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers), BotPerm(GuildPermission.BanMembers)]
        public async Task Unban(ulong userId)
        {
            var bun = await Context.Guild.GetBanAsync(userId);

            if (bun == null)
            {
                await ReplyErrorLocalizedAsync("user_not_found").ConfigureAwait(false);
                return;
            }

            await UnbanInternal(bun.User).ConfigureAwait(false);
        }

        private async Task UnbanInternal(IUser user)
        {
            await ctx.Guild.RemoveBanAsync(user).ConfigureAwait(false);

            await ReplyConfirmLocalizedAsync("unbanned_user", Format.Bold(user.ToString())).ConfigureAwait(false);
        }

        /// <summary>
        /// Softbans a user. Use this to kick a user and delete their messages.
        /// </summary>
        /// <param name="user">The user to softban</param>
        /// <param name="msg">The reason for the softban</param>
        /// <returns></returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.KickMembers | GuildPermission.ManageMessages), BotPerm(GuildPermission.BanMembers)]
        public Task Softban(IGuildUser user, [Remainder] string? msg = null) => SoftbanInternal(user, msg);

        /// <summary>
        /// Softbans a user. Use this to kick a user and delete their messages.
        /// </summary>
        /// <param name="userId">The ID of the user to softban</param>
        /// <param name="msg">The reason for the softban</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.KickMembers | GuildPermission.ManageMessages), BotPerm(GuildPermission.BanMembers)]
        public async Task Softban(ulong userId, [Remainder] string? msg = null)
        {
            var user = await ((DiscordSocketClient)Context.Client).Rest.GetGuildUserAsync(Context.Guild.Id,
                userId).ConfigureAwait(false);
            if (user is null)
                return;

            await SoftbanInternal(user).ConfigureAwait(false);
        }

        private async Task SoftbanInternal(IGuildUser user, [Remainder] string? msg = null)
        {
            if (!await CheckRoleHierarchy(user).ConfigureAwait(false))
                return;

            var dmFailed = false;

            try
            {
                await user.SendErrorAsync(GetText("sbdm", Format.Bold(ctx.Guild.Name), msg)).ConfigureAwait(false);
            }
            catch
            {
                dmFailed = true;
            }

            await ctx.Guild.AddBanAsync(user, 7, options: new RequestOptions
            {
                AuditLogReason = $"Softban: {ctx.User} | {msg}"
            }).ConfigureAwait(false);
            try
            {
                await ctx.Guild.RemoveBanAsync(user).ConfigureAwait(false);
            }
            catch
            {
                await ctx.Guild.RemoveBanAsync(user).ConfigureAwait(false);
            }

            var toSend = new EmbedBuilder().WithOkColor()
                .WithTitle($"☣ {GetText("sb_user")}")
                .AddField(efb => efb.WithName(GetText("username")).WithValue(user.ToString()).WithIsInline(true))
                .AddField(efb => efb.WithName("ID").WithValue(user.Id.ToString()).WithIsInline(true))
                .WithImageUrl((await nekos.ActionsApi.Kick().ConfigureAwait(false)).Results.First().Url);

            if (dmFailed) toSend.WithFooter($"⚠️ {GetText("unable_to_dm_user")}");

            await ctx.Channel.EmbedAsync(toSend)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Kicks a user.
        /// </summary>
        /// <param name="user">The user to kick</param>
        /// <param name="msg">The reason for the kick</param>
        /// <returns></returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.KickMembers), BotPerm(GuildPermission.KickMembers), Priority(1)]
        public Task Kick(IGuildUser user, [Remainder] string? msg = null) => KickInternal(user, msg);

        /// <summary>
        /// Kicks a user.
        /// </summary>
        /// <param name="userId">The ID of the user to kick</param>
        /// <param name="msg">The reason for the kick</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.KickMembers), BotPerm(GuildPermission.KickMembers), Priority(0)]
        public async Task Kick(ulong userId, [Remainder] string? msg = null)
        {
            var user = await ((DiscordSocketClient)Context.Client).Rest.GetGuildUserAsync(Context.Guild.Id,
                userId).ConfigureAwait(false);
            if (user is null)
                return;

            await KickInternal(user, msg).ConfigureAwait(false);
        }

        /// <summary>
        /// Kicks multiple users.
        /// </summary>
        /// <param name="usersUnp">The users to kick</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.KickMembers),
         BotPerm(GuildPermission.KickMembers)]
        public async Task MassKick(params IUser[] usersUnp)
        {
            var users = usersUnp.Cast<IGuildUser>();
            List<ulong> succ = new(), fail = new();

            var options = new RequestOptions
            {
                AuditLogReason = $"Masskick initiated by {Context.User}"
            };

            foreach (var u in users)
                try
                {
                    await u.KickAsync(null, options);
                    succ.Add(u.Id);
                }
                catch
                {
                    fail.Add(u.Id);
                }

            var eb = new EmbedBuilder()
                .WithColor(fail.Count > 0 ? fail.Count > succ.Count ? Color.Red : Color.Orange : Color.Green)
                .WithDescription(GetText("mass_kicked_members", succ.Count))
                .WithTitle(GetText("mass_kick"));
            await Context.Channel.SendMessageAsync(embed: eb.Build());
        }

        private async Task KickInternal(IGuildUser user, string? msg = null)
        {
            if (!await CheckRoleHierarchy(user).ConfigureAwait(false))
                return;

            var dmFailed = false;

            try
            {
                await user.SendErrorAsync(GetText("kickdm", Format.Bold(ctx.Guild.Name), msg))
                    .ConfigureAwait(false);
            }
            catch
            {
                dmFailed = true;
            }

            await user.KickAsync($"{ctx.User} | {msg}").ConfigureAwait(false);

            var toSend = new EmbedBuilder().WithOkColor()
                .WithTitle(GetText("kicked_user"))
                .AddField(efb => efb.WithName(GetText("username")).WithValue(user.ToString()).WithIsInline(true))
                .AddField(efb => efb.WithName("ID").WithValue(user.Id.ToString()).WithIsInline(true))
                .WithImageUrl((await nekos.ActionsApi.Kick().ConfigureAwait(false)).Results.First().Url);

            if (dmFailed) toSend.WithFooter($"⚠️ {GetText("unable_to_dm_user")}");

            await ctx.Channel.EmbedAsync(toSend)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Massbans users. Use this to ban multiple users at once. Blacklists them from the bot as well.
        /// </summary>
        /// <param name="people"></param>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers), BotPerm(GuildPermission.BanMembers), OwnerOnly]
        public async Task MassKill([Remainder] string people)
        {
            if (string.IsNullOrWhiteSpace(people))
                return;

            var (bans, missing) = Service.MassKill((SocketGuild)ctx.Guild, people);

            var missStr = string.Join("\n", missing);
            if (string.IsNullOrWhiteSpace(missStr))
                missStr = "-";

            //send a message but don't wait for it
            var banningMessageTask = ctx.Channel.EmbedAsync(new EmbedBuilder()
                .WithDescription(GetText("mass_kill_in_progress", bans.Count()))
                .AddField(GetText("invalid", missing), missStr)
                .WithOkColor());

            //do the banning
            await Task.WhenAll(bans
                    .Where(x => x.id.HasValue)
                    .Select(x => ctx.Guild.AddBanAsync(x.id.Value, 7, "", new RequestOptions
                    {
                        RetryMode = RetryMode.AlwaysRetry, AuditLogReason = x.Reason
                    })))
                .ConfigureAwait(false);

            //wait for the message and edit it
            var banningMessage = await banningMessageTask.ConfigureAwait(false);

            await banningMessage.ModifyAsync(x => x.Embed = new EmbedBuilder()
                .WithDescription(GetText("mass_kill_completed", bans.Count()))
                .AddField(GetText("invalid", missing), missStr)
                .WithOkColor()
                .Build()).ConfigureAwait(false);
        }
    }
}
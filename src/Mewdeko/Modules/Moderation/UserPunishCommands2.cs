using CommandLine;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Database;
using Mewdeko.Database.Extensions;
using Mewdeko.Database.Models;
using Mewdeko.Modules.Moderation.Services;
using Serilog;
using Swan;

namespace Mewdeko.Modules.Moderation;

public partial class Moderation
{
    [Group]
    public class UserPunishCommands2 : MewdekoSubmodule<UserPunishService2>
    {
        public enum AddRole
        {
            AddRole
        }

        private readonly DbService _db;
        private readonly InteractiveService _interactivity;

        public UserPunishCommands2(DbService db, InteractiveService serv)
        {
            _interactivity = serv;
            _db = db;
        }

        [MewdekoCommand, Usage, Description, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator), Priority(0)]
        public async Task SetMWarnChannel([Remainder] ITextChannel channel)
        {
            if (string.IsNullOrWhiteSpace(channel.Name))
                return;

            if (MWarnlogChannel == channel.Id)
            {
                await ctx.Channel.SendErrorAsync("This is already your mini warnlog channel!");
                return;
            }

            if (MWarnlogChannel == 0)
            {
                await Service.SetMWarnlogChannelId(ctx.Guild, channel);
                var warnChannel = await ctx.Guild.GetTextChannelAsync(MWarnlogChannel);
                await ctx.Channel.SendConfirmAsync($"Your mini warnlog channel has been set to {warnChannel.Mention}");
                return;
            }

            var oldWarnChannel = await ctx.Guild.GetTextChannelAsync(MWarnlogChannel);
            await Service.SetMWarnlogChannelId(ctx.Guild, channel);
            var newWarnChannel = await ctx.Guild.GetTextChannelAsync(MWarnlogChannel);
            await ctx.Channel.SendConfirmAsync(
                $"Your mini warnlog channel has been changed from {oldWarnChannel.Mention} to {newWarnChannel.Mention}");
        }

        [MewdekoCommand, Usage, Description, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.MuteMembers)]
        public async Task MWarn(IGuildUser user, [Remainder] string? reason = null)
        {
            if (ctx.User.Id != user.Guild.OwnerId
                && user.GetRoles().Select(r => r.Position).Max() >=
                ((IGuildUser) ctx.User).GetRoles().Select(r => r.Position).Max())
            {
                await ReplyErrorLocalizedAsync("hierarchy").ConfigureAwait(false);
                return;
            }

            try
            {
                await (await user.CreateDMChannelAsync().ConfigureAwait(false)).EmbedAsync(new EmbedBuilder()
                        .WithErrorColor()
                        .WithDescription($"Warned in {ctx.Guild}")
                        .AddField(efb => efb.WithName(GetText("moderator")).WithValue(ctx.User.ToString()))
                        .AddField(efb => efb.WithName(GetText("reason")).WithValue(reason ?? "-")))
                    .ConfigureAwait(false);
            }
            catch
            {
            }

            WarningPunishment2 punishment;
            try
            {
                punishment = await Service.Warn(ctx.Guild, user.Id, ctx.User, reason).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex.Message);
                await ReplyErrorLocalizedAsync("cant_apply_punishment").ConfigureAwait(false);
                return;
            }

            if (punishment == null)
                await ReplyConfirmLocalizedAsync("user_warned", Format.Bold(user.ToString())).ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("user_warned_and_punished", Format.Bold(user.ToString()),
                    Format.Bold(punishment.Punishment.ToString())).ConfigureAwait(false);
            if (MWarnlogChannel != 0)
            {
                var uow = _db.GetDbContext();
                var warnings = uow.Warnings2
                    .ForId(ctx.Guild.Id, user.Id)
                    .Count(w => !w.Forgiven && w.UserId == user.Id);
                var condition = punishment != null;
                var punishtime = condition ? TimeSpan.FromMinutes(punishment.Time).Humanize() : " ";
                var punishaction = condition ? punishment.Punishment.ToString() : "None";
                var channel = await ctx.Guild.GetTextChannelAsync(MWarnlogChannel);
                await channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                    .WithThumbnailUrl(user.RealAvatarUrl().ToString())
                    .WithTitle($"Mini Warned by: {ctx.User}")
                    .WithCurrentTimestamp()
                    .WithDescription(
                        $"Username: {user.Username}#{user.Discriminator}\nID of Warned User: {user.Id}\nWarn Number: {warnings}\nPunishment: {punishaction} {punishtime}\n\nReason: {reason}\n\n[Click Here For Context](https://discord.com/channels/{ctx.Guild.Id}/{ctx.Channel.Id}/{ctx.Message.Id})"));
            }
        }

        [MewdekoCommand, Usage, Description, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator), MewdekoOptions(typeof(WarnExpireOptions)), Priority(2)]
        public async Task MWarnExpire(int days, params string[] args)
        {
            if (days is < 0 or > 366)
                return;

            var opts = OptionsParser.ParseFrom<WarnExpireOptions>(args);

            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);

            await Service.WarnExpireAsync(ctx.Guild.Id, days, opts.Delete).ConfigureAwait(false);
            if (days == 0)
            {
                await ReplyConfirmLocalizedAsync("warn_expire_reset").ConfigureAwait(false);
                return;
            }

            if (opts.Delete)
                await ReplyConfirmLocalizedAsync("warn_expire_set_delete", Format.Bold(days.ToString()))
                    .ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("warn_expire_set_clear", Format.Bold(days.ToString()))
                    .ConfigureAwait(false);
        }

        [MewdekoCommand, Usage, Description, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.MuteMembers), Priority(2)]
        public Task MWarnlog(int page, IGuildUser user) => MWarnlog(page, user.Id);

        [MewdekoCommand, Usage, Description, Aliases, RequireContext(ContextType.Guild), Priority(3)]
        public Task MWarnlog(IGuildUser? user = null)
        {
            if (user == null)
                user = (IGuildUser) ctx.User;
            return ctx.User.Id == user.Id || ((IGuildUser) ctx.User).GuildPermissions.MuteMembers
                ? MWarnlog(user.Id)
                : Task.CompletedTask;
        }

        [MewdekoCommand, Usage, Description, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.MuteMembers), Priority(0)]
        public Task MWarnlog(int page, ulong userId) => InternalWarnlog(userId, page - 1);

        [MewdekoCommand, Usage, Description, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.MuteMembers), Priority(1)]
        public Task MWarnlog(ulong userId) => InternalWarnlog(userId, 0);

        private async Task InternalWarnlog(ulong userId, int page)
        {
            if (page < 0)
                return;
            var warnings = Service.UserWarnings(ctx.Guild.Id, userId);

            warnings = warnings.Skip(page * 9)
                .Take(9)
                .ToArray();

            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle(GetText("warnlog_for",
                    (ctx.Guild as SocketGuild)?.GetUser(userId)?.ToString() ?? userId.ToString()))
                .WithFooter(efb => efb.WithText(GetText("page", page + 1)));

            if (!warnings.Any())
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

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [MewdekoCommand, Usage, Description, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.MuteMembers)]
        public async Task MWarnlogAll(int page = 1)
        {
            if (--page < 0)
                return;
            var warnings = Service.WarnlogAll(ctx.Guild.Id);

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(warnings.Length / 15)
                .WithDefaultEmotes()
                .Build();

            await _interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60));

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask;
                {
                    var ws = warnings.Skip(page * 15)
                        .Take(15)
                        .ToArray()
                        .Select(x =>
                        {
                            var all = x.Count();
                            var forgiven = x.Count(y => y.Forgiven);
                            var total = all - forgiven;
                            var usr = ((SocketGuild) ctx.Guild).GetUser(x.Key);
                            return $"{(usr?.ToString() ?? x.Key.ToString())} | {total} ({all} - {forgiven})";
                        });

                    return new PageBuilder().WithOkColor()
                        .WithTitle(GetText("warnings_list"))
                        .WithDescription(string.Join("\n", ws));
                }
            }
        }

        [MewdekoCommand, Usage, Description, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator)]
        public Task MWarnclear(IGuildUser user, int index = 0) => MWarnclear(user.Id, index);

        [MewdekoCommand, Usage, Description, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator)]
        public async Task MWarnclear(ulong userId, int index = 0)
        {
            if (index < 0)
                return;
            var success = await Service.WarnClearAsync(ctx.Guild.Id, userId, index, ctx.User.ToString());
            var userStr = Format.Bold((ctx.Guild as SocketGuild)?.GetUser(userId)?.ToString() ?? userId.ToString());
            if (index == 0)
            {
                await ReplyConfirmLocalizedAsync("warnings_cleared", userStr).ConfigureAwait(false);
            }
            else
            {
                if (success)
                    await ReplyConfirmLocalizedAsync("warning_cleared", Format.Bold(index.ToString()), userStr)
                        .ConfigureAwait(false);
                else
                    await ReplyErrorLocalizedAsync("warning_clear_fail").ConfigureAwait(false);
            }
        }

        [MewdekoCommand, Usage, Description, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator), Priority(1)]
        public async Task MWarnPunish(int number, AddRole _, IRole role, StoopidTime? time = null)
        {
            var punish = PunishmentAction.AddRole;
            var success = Service.WarnPunish(ctx.Guild.Id, number, punish, time, role);

            if (!success)
                return;

            if (time is null)
                await ReplyConfirmLocalizedAsync("warn_punish_set",
                    Format.Bold(punish.ToString()),
                    Format.Bold(number.ToString())).ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("warn_punish_set_timed",
                    Format.Bold(punish.ToString()),
                    Format.Bold(number.ToString()),
                    Format.Bold(time.Input)).ConfigureAwait(false);
        }

        [MewdekoCommand, Usage, Description, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator)]
        public async Task MWarnPunish(int number, PunishmentAction punish, StoopidTime? time = null)
        {
            // this should never happen. Addrole has its own method with higher priority
            if (punish == PunishmentAction.AddRole)
                return;

            var success = Service.WarnPunish(ctx.Guild.Id, number, punish, time);

            if (!success)
                return;

            if (time is null)
                await ReplyConfirmLocalizedAsync("warn_punish_set",
                    Format.Bold(punish.ToString()),
                    Format.Bold(number.ToString())).ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("warn_punish_set_timed",
                    Format.Bold(punish.ToString()),
                    Format.Bold(number.ToString()),
                    Format.Bold(time.Input)).ConfigureAwait(false);
        }

        [MewdekoCommand, Usage, Description, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator)]
        public async Task MWarnPunish(int number)
        {
            if (!Service.WarnPunishRemove(ctx.Guild.Id, number)) return;

            await ReplyConfirmLocalizedAsync("warn_punish_rem",
                Format.Bold(number.ToString())).ConfigureAwait(false);
        }

        [MewdekoCommand, Usage, Description, Aliases, RequireContext(ContextType.Guild)]
        public async Task MWarnPunishList()
        {
            var ps = Service.WarnPunishList(ctx.Guild.Id);

            string list;
            if (ps.Any())
                list = string.Join("\n",
                    ps.Select(x =>
                        $"{x.Count} -> {x.Punishment} {(x.Punishment == PunishmentAction.AddRole ? $"<@&{x.RoleId}>" : "")} {(x.Time <= 0 ? "" : $"{x.Time}m")} "));
            else
                list = GetText("warnpl_none");
            await ctx.Channel.SendConfirmAsync(
                GetText("warn_punish_list"),
                list).ConfigureAwait(false);
        }

        public class WarnExpireOptions : IMewdekoCommandOptions
        {
            [Option('d', "delete", Default = false, HelpText = "Delete warnings instead of clearing them.")]
            public bool Delete { get; set; } = false;

            public void NormalizeOptions()
            {
            }
        }
    }
}
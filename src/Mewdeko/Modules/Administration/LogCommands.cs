﻿using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Administration.Services;
using static Mewdeko.Modules.Administration.Services.LogCommandService;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    [Group]
    public class LogCommands : MewdekoSubmodule<LogCommandService>
    {
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator), Priority(1)]
        public async Task LogCategory(LogCategoryTypes type, ITextChannel? channel = null)
        {
            await Service.LogSetByType(ctx.Guild.Id, channel?.Id ?? 0, type);
            if (channel is null)
            {
                await ctx.Channel.SendConfirmAsync(GetText("logging_category_disabled", type));
                return;
            }

            await ctx.Channel.SendConfirmAsync(GetText("logging_category_enabled", type, channel.Mention));
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator), Priority(0)]
        public async Task LogIgnore()
        {
            var channel = (ITextChannel)ctx.Channel;

            var removed = await Service.LogIgnore(ctx.Guild.Id, ctx.Channel.Id);

            if (!removed)
                await ReplyConfirmLocalizedAsync("log_ignore", Format.Bold($"{channel.Mention}({channel.Id})")).ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("log_not_ignore", Format.Bold($"{channel.Mention}({channel.Id})")).ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator), Priority(1)]
        public async Task LogIgnore(ITextChannel channel)
        {
            var removed = await Service.LogIgnore(ctx.Guild.Id, channel.Id);

            if (!removed)
                await ReplyConfirmLocalizedAsync("log_ignore", Format.Bold($"{channel.Mention}({channel.Id})")).ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("log_not_ignore", Format.Bold($"{channel.Mention}({channel.Id})")).ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator), Priority(2)]
        public async Task LogIgnore(IVoiceChannel channel)
        {
            var removed = await Service.LogIgnore(ctx.Guild.Id, channel.Id);

            if (!removed)
                await ReplyConfirmLocalizedAsync("log_ignore", Format.Bold($"{channel.Name}({channel.Id})")).ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("log_not_ignore", Format.Bold($"{channel.Name}({channel.Id})")).ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task LogEvents()
        {
            Service.GuildLogSettings.TryGetValue(ctx.Guild.Id, out var l);
            var str = string.Join("\n", Enum.GetNames(typeof(LogType)).OrderBy(x => x).Select(x =>
            {
                var val = l == null ? null : GetLogProperty(l, Enum.Parse<LogType>(x));
                return val != null && val != 0 ? $"{Format.Bold(x)} <#{val}>" : Format.Bold(x);
            }));

            await ctx.Channel.SendConfirmAsync($"{Format.Bold(GetText("log_events"))}\n{str}").ConfigureAwait(false);
        }

        private static ulong? GetLogProperty(LogSetting l, LogType type) =>
            type switch
            {
                LogType.Other => l.LogOtherId,
                LogType.MessageUpdated => l.MessageUpdatedId,
                LogType.UserUpdated => l.UserUpdatedId,
                LogType.MessageDeleted => l.MessageDeletedId,
                LogType.UserJoined => l.UserJoinedId,
                LogType.UserLeft => l.UserLeftId,
                LogType.UserBanned => l.UserBannedId,
                LogType.UserUnbanned => l.UserUnbannedId,
                LogType.ChannelCreated => l.ChannelCreatedId,
                LogType.ChannelDestroyed => l.ChannelDestroyedId,
                LogType.ChannelUpdated => l.ChannelUpdatedId,
                LogType.VoicePresence => l.LogVoicePresenceId,
                LogType.VoicePresenceTts => l.LogVoicePresenceTTSId,
                LogType.UserMuted => l.UserMutedId,
                LogType.EventCreated => l.EventCreatedId,
                LogType.ThreadCreated => l.ThreadCreatedId,
                LogType.ThreadDeleted => l.ThreadDeletedId,
                LogType.ThreadUpdated => l.ThreadUpdatedId,
                LogType.NicknameUpdated => l.NicknameUpdatedId,
                LogType.RoleCreated => l.RoleCreatedId,
                LogType.RoleDeleted => l.RoleDeletedId,
                LogType.RoleUpdated => l.RoleUpdatedId,
                LogType.ServerUpdated => l.ServerUpdatedId,
                LogType.UserRoleAdded => l.UserRoleAddedId,
                LogType.UserRoleRemoved => l.UserRoleRemovedId,
                LogType.UsernameUpdated => l.UsernameUpdatedId,


                _ => null
            };

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator), Priority(0)]
        public async Task Log(LogType type)
        {
            await Service.SetLogChannel(ctx.Guild.Id, ctx.Channel.Id, type).ConfigureAwait(false);
            await ReplyConfirmLocalizedAsync("log", Format.Bold(type.ToString())).ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator), Priority(1)]
        public async Task Log(LogType type, ITextChannel? channel = null)
        {
            await Service.SetLogChannel(ctx.Guild.Id, channel?.Id ?? 0, type).ConfigureAwait(false);
            if (channel is not null)
            {
                await ConfirmLocalizedAsync("logging_event_enabled", type, channel.Id).ConfigureAwait(false);
                return;
            }

            await ConfirmLocalizedAsync("logging_event_disabled", type);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task CommandLogChannel(ITextChannel? channel = null)
        {
            if (channel is null)
            {
                await ConfirmLocalizedAsync("command_logging_disabled");
                await Service.UpdateCommandLogChannel(ctx.Guild, 0);
            }
            else
            {
                await ConfirmLocalizedAsync("command_logging_enabled");
                await Service.UpdateCommandLogChannel(ctx.Guild, channel.Id);
            }
        }
    }
}
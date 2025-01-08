using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Administration.Services;
using LogType = Mewdeko.Modules.Administration.Services.LogCommandService.LogType;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    /// <summary>
    ///     Module for logging commands.
    /// </summary>
    [Group]
    public class LogCommands(GuildSettingsService gss) : MewdekoSubmodule<LogCommandService>
    {
        /// <summary>
        ///     Sets the logging category for a specified type of logs.
        /// </summary>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        /// <param name="type">The type of logs to set the category for.</param>
        /// <param name="channel">The text channel where the logs will be sent.</param>
        /// <example>.logcategory messages #log-channel</example>
        /// <example>.logcategory messages</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        [Priority(1)]
        public async Task LogCategory(LogCommandService.LogCategoryTypes type, ITextChannel? channel = null)
        {
            await Service.LogSetByType(ctx.Guild.Id, channel?.Id ?? 0, type);
            if (channel is null)
            {
                await ctx.Channel.SendConfirmAsync(Strings.LoggingCategoryDisabled(ctx.Guild.Id, type));
                return;
            }

            await ctx.Channel.SendConfirmAsync(Strings.LoggingCategoryEnabled(ctx.Guild.Id, type, channel.Mention));
        }


        // [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator), Priority(0)]
        // public async Task LogIgnore()
        // {
        //     var channel = (ITextChannel)ctx.Channel;
        //
        //     var removed = await Service.LogIgnore(ctx.Guild.Id, ctx.Channel.Id);
        //
        //     if (!removed)
        //         await ReplyConfirmAsync(Strings.LogIgnore(ctx.Guild.Id, Format.Bold($"{channel.Mention}({channel.Id}))")).ConfigureAwait(false);
        //     else
        //         await ReplyConfirmAsync(Strings.LogNotIgnore(ctx.Guild.Id, Format.Bold($"{channel.Mention}({channel.Id}))")).ConfigureAwait(false);
        // }
        //
        // [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator), Priority(1)]
        // public async Task LogIgnore(ITextChannel channel)
        // {
        //     var removed = await Service.LogIgnore(ctx.Guild.Id, channel.Id);
        //
        //     if (!removed)
        //         await ReplyConfirmAsync(Strings.LogIgnore(ctx.Guild.Id, Format.Bold($"{channel.Mention}({channel.Id}))")).ConfigureAwait(false);
        //     else
        //         await ReplyConfirmAsync(Strings.LogNotIgnore(ctx.Guild.Id, Format.Bold($"{channel.Mention}({channel.Id}))")).ConfigureAwait(false);
        // }
        //
        // [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator), Priority(2)]
        // public async Task LogIgnore(IVoiceChannel channel)
        // {
        //     var removed = await Service.LogIgnore(ctx.Guild.Id, channel.Id);
        //
        //     if (!removed)
        //         await ReplyConfirmAsync(Strings.LogIgnore(ctx.Guild.Id, Format.Bold($"{channel.Name}({channel.Id}))")).ConfigureAwait(false);
        //     else
        //         await ReplyConfirmAsync(Strings.LogNotIgnore(ctx.Guild.Id, Format.Bold($"{channel.Name}({channel.Id}))")).ConfigureAwait(false);
        // }

        /// <summary>
        ///     Displays the current logging events configuration for the guild.
        /// </summary>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        /// <example>.logevents</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task LogEvents()
        {
            Service.GuildLogSettings.TryGetValue(ctx.Guild.Id, out var l);
            var str = string.Join("\n", Enum.GetNames(typeof(LogType)).OrderBy(x => x).Select(x =>
            {
                var val = l == null ? null : GetLogProperty(l, Enum.Parse<LogType>(x));
                return val != null && val != 0 ? $"{Format.Bold(x)} <#{val}>" : Format.Bold(x);
            }));

            await ctx.Channel.SendConfirmAsync($"{Format.Bold(Strings.LogEvents(ctx.Guild.Id))}\n{str}").ConfigureAwait(false);
        }


        private static ulong? GetLogProperty(LoggingV2 l, LogType type)
        {
            return type switch
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
                LogType.VoicePresenceTts => l.LogVoicePresenceTtsId,
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
        }


        /// <summary>
        ///     Sets the logging channel for a specific event type.
        /// </summary>
        /// <param name="type">The type of event to set the logging channel for.</param>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        /// <param name="channel">
        ///     The channel to set as the logging channel. If not provided, the command will disable logging for
        ///     the specified event type.
        /// </param>
        /// <example>.log UserJoined #log-channel</example>
        /// <example>.log UserLeft</example>
        /// <example>.log UserJoined</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        [Priority(0)]
        public async Task Log(LogType type, ITextChannel? channel = null)
        {
            try
            {
                channel ??= ctx.Channel as ITextChannel;
                await Service.SetLogChannel(ctx.Guild.Id, channel?.Id ?? 0, type).ConfigureAwait(false);
                if (channel is not null)
                {
                    await ConfirmAsync(Strings.LoggingEventEnabled(ctx.Guild.Id, type, channel.Id)).ConfigureAwait(false);
                    return;
                }

                await ConfirmAsync(Strings.LoggingEventDisabled(ctx.Guild.Id, type));
            }
            catch (Exception e)
            {
                Serilog.Log.Error(e, "There was an issue setting logs");
                await ErrorAsync(Strings.CommandFatalError(ctx.Guild.Id));
            }
        }


        /// <summary>
        ///     Sets a channel to log commands to
        /// </summary>
        /// <param name="channel">The channel to log commands to</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task CommandLogChannel(ITextChannel? channel = null)
        {
            if (channel is null)
            {
                await ConfirmAsync(Strings.CommandLoggingDisabled(ctx.Guild.Id));
                var gc = await gss.GetGuildConfig(ctx.Guild.Id);
                gc.CommandLogChannel = 0;
                await gss.UpdateGuildConfig(ctx.Guild.Id, gc);
            }
            else
            {
                await ConfirmAsync(Strings.CommandLoggingEnabled(ctx.Guild.Id));
                var gc = await gss.GetGuildConfig(ctx.Guild.Id);
                gc.CommandLogChannel = channel.Id;
                await gss.UpdateGuildConfig(ctx.Guild.Id, gc);
            }
        }
    }
}
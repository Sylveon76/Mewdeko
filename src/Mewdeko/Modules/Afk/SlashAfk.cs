﻿using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Afk.Services;

namespace Mewdeko.Modules.Afk;

/// <summary>
/// Slash commands for setting and managing AFK messages.
/// </summary>
[Group("afk", "Set or Manage AFK")]
public class SlashAfk : MewdekoSlashModuleBase<AfkService>
{
    private readonly InteractiveService interactivity;
    private readonly DiscordSocketClient client;

    /// <summary>
    /// Initializes a new instance of <see cref="SlashAfk"/>.
    /// </summary>
    /// <param name="serv">The interactivity service used for embed pagination.</param>
    /// <param name="client">The discord client.</param>
    public SlashAfk(InteractiveService serv, DiscordSocketClient client)
    {
        interactivity = serv;
        this.client = client;
    }

    /// <summary>
    /// Sets the user's AFK status with an optional message.
    /// </summary>
    /// <param name="message">The AFK message. If not provided, the user's AFK status will be toggled.</param>
    /// <example>.afk</example>
    /// <example>.afk I'm AFK</example>
    [SlashCommand("set", "Set your afk with an optional message"), RequireContext(ContextType.Guild), CheckPermissions,
     SlashUserPerm(GuildPermission.SendMessages)]
    public async Task Afk(string? message = null)
    {
        if (Environment.GetEnvironmentVariable($"AFK_CACHED_{client.ShardId}") != "1")
        {
            await ErrorLocalizedAsync("afk_still_starting");
            return;
        }

        if (message == null)
        {
            var afkmsg = (await Service.GetAfk(ctx.Guild.Id, ctx.User.Id))?.Message;
            if (string.IsNullOrEmpty(afkmsg))
            {
                await Service.AfkSet(ctx.Guild.Id, ctx.User.Id, "_ _", 0).ConfigureAwait(false);
                await EphemeralReplyErrorLocalizedAsync("afk_msg_enabled").ConfigureAwait(false);
                try
                {
                    var user = await ctx.Guild.GetUserAsync(ctx.User.Id).ConfigureAwait(false);
                    var toset = user.Nickname is null
                        ? $"[AFK] {user.Username.TrimTo(26)}"
                        : $"[AFK] {user.Nickname.TrimTo(26)}";
                    await user.ModifyAsync(x => x.Nickname = toset).ConfigureAwait(false);
                }
                catch
                {
                    //ignored
                }

                await ctx.Guild.DownloadUsersAsync().ConfigureAwait(false);
                return;
            }

            await Service.AfkSet(ctx.Guild.Id, ctx.User.Id, "", 0).ConfigureAwait(false);
            await EphemeralReplyConfirmLocalizedAsync("afk_msg_disabled");
            try
            {
                var user = await ctx.Guild.GetUserAsync(ctx.User.Id).ConfigureAwait(false);
                await user.ModifyAsync(x => x.Nickname = user.Nickname.Replace("[AFK]", "")).ConfigureAwait(false);
            }
            catch
            {
                //ignored
            }

            await ctx.Guild.DownloadUsersAsync().ConfigureAwait(false);
            return;
        }

        if (message.Length != 0 && message.Length > await Service.GetAfkLength(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("afk_message_too_long", Service.GetAfkLength(ctx.Guild.Id))
                .ConfigureAwait(false);
            return;
        }

        await Service.AfkSet(ctx.Guild.Id, ctx.User.Id, message.EscapeWeirdStuff(), 0).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("afk_enabled", message).ConfigureAwait(false);
        await ctx.Guild.DownloadUsersAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Sets a timed AFK status with a custom message.
    /// </summary>
    /// <param name="time">The duration for the AFK status.</param>
    /// <param name="message">The custom message for the AFK status.</param>
    /// <example>.afk 1h I'm AFK</example>
    [SlashCommand("timed", "Sets a timed afk that auto removes itself and pings you when it."),
     RequireContext(ContextType.Guild), CheckPermissions, SlashUserPerm(GuildPermission.SendMessages)]
    public async Task TimedAfk(string time, string message)
    {
        if (Environment.GetEnvironmentVariable($"AFK_CACHED_{client.ShardId}") != "1")
        {
            await EphemeralReplyErrorLocalizedAsync("afk_still_starting").ConfigureAwait(false);
            return;
        }

        var parsedTime = StoopidTime.FromInput(time);
        if (parsedTime.Time.Equals(default))
        {
            await EphemeralReplyErrorLocalizedAsync("afk_time_invalid").ConfigureAwait(false);
            return;
        }

        if (message.Length != 0 && message.Length > await Service.GetAfkLength(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("afk_message_too_long", Service.GetAfkLength(ctx.Guild.Id))
                .ConfigureAwait(false);
            return;
        }

        await Service.AfkSet(ctx.Guild.Id, ctx.User.Id, message, 1, DateTime.UtcNow + parsedTime.Time);
        await ConfirmLocalizedAsync("afk_time_set",
            TimestampTag.FromDateTimeOffset(DateTimeOffset.UtcNow + parsedTime.Time), TimestampTagStyles.Relative,
            message);
    }

    /// <summary>
    /// Sets a custom AFK embed that will be displayed when a user is AFK. Use "-" to reset to the default embed. Check https://eb.mewdeko.tech for the embed builder and http://mewdeko.tech/placeholders for placeholders.
    /// </summary>
    /// <param name="embedCode">The custom message to set. Use "-" to reset to the default message.</param>
    /// <example>/afk message -</example>
    /// <example>/afk message embedcode</example>
    [SlashCommand("message", "Allows you to set a custom embed for AFK messages."), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task CustomAfkMessage(string embedCode)
    {
        if (Environment.GetEnvironmentVariable($"AFK_CACHED_{client.ShardId}") != "1")
        {
            await ErrorLocalizedAsync("afk_still_starting").ConfigureAwait(false);
            return;
        }

        if (embedCode == "-")
        {
            await Service.SetCustomAfkMessage(ctx.Guild, "-").ConfigureAwait(false);
            await ConfirmLocalizedAsync("afk_message_default").ConfigureAwait(false);
            return;
        }

        await Service.SetCustomAfkMessage(ctx.Guild, embedCode).ConfigureAwait(false);
        await ConfirmLocalizedAsync("Sucessfully updated afk message!").ConfigureAwait(false);
    }

    /// <summary>
    /// Displays a list of active users who are currently AFK.
    /// </summary>
    /// <example>./afk listactive</example>
    [SlashCommand("listactive", "Sends a list of active afk users"), CheckPermissions]
    public async Task GetActiveAfks()
    {
        if (Environment.GetEnvironmentVariable($"AFK_CACHED_{client.ShardId}") != "1")
        {
            await ErrorLocalizedAsync("afk_still_starting").ConfigureAwait(false);
            return;
        }

        var afks = await Service.GetAfkUsers(ctx.Guild).ConfigureAwait(false);
        if (afks.Count == 0)
        {
            await ErrorLocalizedAsync("afk_user_none").ConfigureAwait(false);
            return;
        }

        var paginator = new LazyPaginatorBuilder().AddUser(ctx.User).WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(afks.ToArray().Length / 20).WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage).Build();

        await interactivity
            .SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!, TimeSpan.FromMinutes(60))
            .ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return new PageBuilder().WithOkColor().WithTitle($"{Format.Bold("Active AFKs")} - {afks.ToArray().Length}")
                .WithDescription(string.Join("\n", afks.ToArray().Skip(page * 20).Take(20)));
        }
    }

    /// <summary>
    /// Displays the AFK status of a specific user.
    /// </summary>
    /// <param name="user">The user to check the AFK status for.</param>
    /// <example>/afk view @user</example>
    [SlashCommand("view", "View another user's afk message"), SlashUserPerm(GuildPermission.ManageMessages),
     CheckPermissions]
    public async Task AfkView(IGuildUser user)
    {
        if (Environment.GetEnvironmentVariable($"AFK_CACHED_{client.ShardId}") != "1")
        {
            await ErrorLocalizedAsync("afk_still_starting").ConfigureAwait(false);
            return;
        }

        if (!await Service.IsAfk(user.Guild.Id, user.Id))
        {
            await ErrorLocalizedAsync("afk_user_none").ConfigureAwait(false);
            return;
        }

        var msg = await Service.GetAfk(user.Guild.Id, user.Id);
        await ctx.Interaction.SendConfirmAsync($"{user}'s Afk is:\n{msg.Message}").ConfigureAwait(false);
    }

    /// <summary>
    /// Lists the text channels where the AFK message doesnt display.
    /// </summary>
    /// <example>/afk disabledlist</example>
    [SlashCommand("disabledlist", "Shows a list of channels where afk messages are not allowed to display"),
     SlashUserPerm(GuildPermission.ManageChannels), CheckPermissions]
    public async Task AfkDisabledList()
    {
        if (Environment.GetEnvironmentVariable($"AFK_CACHED_{client.ShardId}") != "1")
        {
            await ErrorLocalizedAsync("afk_still_starting").ConfigureAwait(false);
            return;
        }

        var mentions = new List<string>();
        var chans = await Service.GetDisabledAfkChannels(ctx.Guild.Id);
        if (string.IsNullOrEmpty(chans) || chans.Contains('0'))
        {
            await ErrorLocalizedAsync("afk_disabled_channels_none").ConfigureAwait(false);
            return;
        }

        await ctx.Interaction.SendConfirmAsync("Loading...").ConfigureAwait(false);
        foreach (var i in chans.Split(","))
        {
            var role = await ctx.Guild.GetTextChannelAsync(Convert.ToUInt64(i)).ConfigureAwait(false);
            mentions.Add(role.Mention);
        }

        var paginator = new LazyPaginatorBuilder().AddUser(ctx.User).WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(mentions.ToArray().Length / 20).WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage).Build();
        await ctx.Interaction.DeleteOriginalResponseAsync().ConfigureAwait(false);
        await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
            .ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return new PageBuilder().WithOkColor()
                .WithTitle($"{Format.Bold("Disabled Afk Channels")} - {mentions.ToArray().Length}")
                .WithDescription(string.Join("\n", mentions.ToArray().Skip(page * 20).Take(20)));
        }
    }

    /// <summary>
    /// Sets the maximum length of all AFK messages.
    /// </summary>
    /// <param name="num">The maximum length you want to set.</param>
    /// <example>/afk maxlength 100</example>
    [SlashCommand("maxlength", "Sets the maximum length of afk messages."),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task AfkLength(int num)
    {
        if (Environment.GetEnvironmentVariable($"AFK_CACHED_{client.ShardId}") != "1")
        {
            await ErrorLocalizedAsync("afk_still_starting").ConfigureAwait(false);
            return;
        }

        if (num > 4096)
        {
            await ErrorLocalizedAsync("afk_max_length_set").ConfigureAwait(false);
        }
        else
        {
            await Service.AfkLengthSet(ctx.Guild, num).ConfigureAwait(false);
            await ConfirmLocalizedAsync("afk_max_length_set", num);
        }
    }

    /// <summary>
    /// Sets the type of AFK behavior for the guild.
    /// </summary>
    /// <param name="afkTypeEnum">The type of AFK behavior to set. <see cref="Afk.AfkTypeEnum"/></param>
    /// <example>.afktype 1</example>
    [SlashCommand("type", "Sets how afk messages are removed. Do @Mewdeko help afktype to see more."),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task AfkType(Afk.AfkTypeEnum afkTypeEnum)
    {
        if (Environment.GetEnvironmentVariable($"AFK_CACHED_{client.ShardId}") != "1")
        {
            await ReplyErrorLocalizedAsync("afk_still_starting").ConfigureAwait(false);
            return;
        }

        await Service.AfkTypeSet(ctx.Guild, (int)afkTypeEnum).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("afk_type_set", afkTypeEnum).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the timeout duration before a user is no longer considered afk. Triggers when a user sends a message or types in a channel.
    /// </summary>
    /// <param name="input">The timeout duration for the AFK status.</param>
    /// <example>/afk timeout 1h</example>
    [SlashCommand("timeout", "Sets after how long mewdeko no longer ignores a user's typing/messages."),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task AfkTimeout(string input)
    {
        if (Environment.GetEnvironmentVariable($"AFK_CACHED_{client.ShardId}") != "1")
        {
            await ErrorLocalizedAsync("afk_still_starting").ConfigureAwait(false);
            return;
        }

        var time = StoopidTime.FromInput(input);
        if (time.Time.Equals(default))
        {
            await ErrorLocalizedAsync("afk_time_invalid");
            return;
        }

        if (time.Time < TimeSpan.FromSeconds(1) || time.Time > TimeSpan.FromHours(2))
        {
            await ErrorLocalizedAsync("afk_timeout_invalid").ConfigureAwait(false);
            return;
        }

        await Service.AfkTimeoutSet(ctx.Guild, Convert.ToInt32(time.Time.TotalSeconds)).ConfigureAwait(false);
        await ConfirmLocalizedAsync("afk_timeout_set", time.Time.Humanize()).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes the specified channel from the afk message blacklist.
    /// </summary>
    /// <param name="channel">The text channel for which to remove from the afk message blacklist.</param>
    /// <example>/afk undisable #channel</example>
    [SlashCommand("undisable", "Allows afk messages to be shown in a channel again."),
     SlashUserPerm(GuildPermission.ManageChannels), CheckPermissions]
    public async Task AfkUndisable(ITextChannel channel)
    {
        if (Environment.GetEnvironmentVariable($"AFK_CACHED_{client.ShardId}") != "1")
        {
            await ctx.Interaction
                .SendErrorAsync("afk_still_starting").ConfigureAwait(false);
            return;
        }

        var chan = new[]
        {
            channel
        };
        var mentions = new List<string>();
        var toremove = new List<string>();
        var chans = await Service.GetDisabledAfkChannels(ctx.Guild.Id);
        if (string.IsNullOrWhiteSpace(chans) || chans == "0")
        {
            await ErrorLocalizedAsync("afk_disabled_channels_none").ConfigureAwait(false);
            return;
        }

        var e = chans.Split(",");
        var list = e.ToList();
        foreach (var i in chan)
        {
            if (e.Contains(i.Id.ToString()))
            {
                toremove.Add(i.Id.ToString());
                mentions.Add(i.Mention);
            }
        }

        if (mentions.Count == 0)
        {
            await ErrorLocalizedAsync("afk_disabled_channels_noneset").ConfigureAwait(false);
            return;
        }

        if (!list.Except(toremove).Any())
        {
            await Service.AfkDisabledSet(ctx.Guild, "0").ConfigureAwait(false);
            await ConfirmLocalizedAsync("afk_ignoring_no_longer_disabled").ConfigureAwait(false);
            return;
        }

        await Service.AfkDisabledSet(ctx.Guild, string.Join(",", list.Except(toremove))).ConfigureAwait(false);
        await ConfirmLocalizedAsync("afk_disabled_channels_removed", string.Join(",", mentions));
    }

    /// <summary>
    /// Sets the channel where the AFK message will not display.
    /// </summary>
    /// <param name="channel">Channel you want to add to the afk message blacklist.</param>
    /// <example>/afk disable #channel</example>
    [SlashCommand("disable", "Disables afk messages to be shown in channels you specify."),
     SlashUserPerm(GuildPermission.ManageChannels), CheckPermissions]
    public async Task AfkDisable(ITextChannel channel)
    {
        if (Environment.GetEnvironmentVariable($"AFK_CACHED_{client.ShardId}") != "1")
        {
            await ctx.Interaction
                .SendErrorAsync("afk_still_starting").ConfigureAwait(false);
            return;
        }

        var chan = new[]
        {
            channel
        };
        var list = new HashSet<string>();
        // ReSharper disable once CollectionNeverQueried.Local
        var newchans = new HashSet<string>();
        var mentions = new HashSet<string>();
        if (await Service.GetDisabledAfkChannels(ctx.Guild.Id) == "0"
            || string.IsNullOrWhiteSpace(await Service.GetDisabledAfkChannels(ctx.Guild.Id)))
        {
            foreach (var i in chan)
            {
                list.Add(i.Id.ToString());
                mentions.Add(i.Mention);
            }

            await Service.AfkDisabledSet(ctx.Guild, string.Join(",", list)).ConfigureAwait(false);
            await ConfirmLocalizedAsync("afk_disabled_in", string.Join(",", list)).ConfigureAwait(false);
        }
        else
        {
            var e = await Service.GetDisabledAfkChannels(ctx.Guild.Id);
            var w = e.Split(",");
            foreach (var i in w)
                list.Add(i);

            foreach (var i in chan)
            {
                if (!w.Contains(i.Id.ToString()))
                {
                    list.Add(i.Id.ToString());
                    mentions.Add(i.Mention);
                }

                newchans.Add(i.Id.ToString());
            }

            if (mentions.Count > 0)
            {
                await ErrorLocalizedAsync("afk_already_in_list").ConfigureAwait(false);
                return;
            }

            await Service.AfkDisabledSet(ctx.Guild, string.Join(",", list)).ConfigureAwait(false);
            await ConfirmLocalizedAsync("afk_channels_updated", string.Join(",", mentions)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Removes the AFK status for a user.
    /// </summary>
    /// <param name="user">The user you want to remove afk from.</param>
    /// <example>/afk remove @user</example>
    [SlashCommand("remove", "Removes afk from a user"), SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions]
    public async Task AfkRemove(IGuildUser user)
    {
        if (!await CheckRoleHierarchy(user))
            return;
        if (Environment.GetEnvironmentVariable($"AFK_CACHED_{client.ShardId}") != "1")
        {
            await ErrorLocalizedAsync("afk_still_starting").ConfigureAwait(false);
            return;
        }

        if (Environment.GetEnvironmentVariable("AFK_CACHED") != "1")
        {
            await ErrorLocalizedAsync("afk_still_starting");
            return;
        }

        var msg = await Service.GetAfk(ctx.Guild.Id, user.Id);
        if (msg is null)
        {
            await EphemeralReplyErrorLocalizedAsync("afk_not_l_bozo").ConfigureAwait(false);
            return;
        }

        await Service.AfkSet(ctx.Guild.Id, user.Id, "", 0).ConfigureAwait(false);
        await EphemeralReplyErrorLocalizedAsync("afk_noted", user.Mention);
    }
}
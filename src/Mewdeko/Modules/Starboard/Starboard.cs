using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Starboard.Services;

namespace Mewdeko.Modules.Starboard;

/// <summary>
///     Module for managing starboard settings.
/// </summary>
public class Starboard(GuildSettingsService guildSettings) : MewdekoSubmodule<StarboardService>
{
    /// <summary>
    ///     Enum representing the mode for whitelisting or blacklisting channels for starboard.
    /// </summary>
    public enum WhitelistMode
    {
        /// <summary>
        ///     Whitelist mode. Only whitelisted channels are checked for starboard posts.
        /// </summary>
        Whitelist = 0,

        /// <summary>
        ///     Alias for Whitelist mode.
        /// </summary>
        Wl = 0,

        /// <summary>
        ///     Alias for Whitelist mode.
        /// </summary>
        White = 0,

        /// <summary>
        ///     Blacklist mode. Blacklisted channels are not checked for starboard posts.
        /// </summary>
        Blacklist = 1,

        /// <summary>
        ///     Alias for Blacklist mode.
        /// </summary>
        Bl = 1,

        /// <summary>
        ///     Alias for Blacklist mode.
        /// </summary>
        Black = 1
    }

    /// <summary>
    ///     Sets the starboard channel for the guild.
    /// </summary>
    /// <param name="chn">The starboard channel. Pass null to disable starboard.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task SetStarboard(ITextChannel? chn = null)
    {
        if (chn is null)
        {
            await Service.SetStarboardChannel(ctx.Guild, 0).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.StarboardDisabled(ctx.Guild.Id));
            return;
        }

        await Service.SetStarboardChannel(ctx.Guild, chn.Id).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync($"Channel set to {chn.Mention}").ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the repost threshold for starboard posts in the guild.
    /// </summary>
    /// <param name="num">The repost threshold. Pass 0 to disable reposting.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task SetRepostThreshold(int num)
    {
        if (num == 0)
        {
            await ctx.Channel.SendErrorAsync(Strings.RepostingDisabled(ctx.Guild.Id), Config);
            await Service.SetRepostThreshold(ctx.Guild, 0).ConfigureAwait(false);
            return;
        }

        await Service.SetRepostThreshold(ctx.Guild, num).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync($"Successfully set the Repost Threshold to {num}").ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the star count required for a message to be added to the starboard in the guild.
    /// </summary>
    /// <param name="num">The star count required.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task SetStars(int num)
    {
        var count = await Service.GetStarCount(ctx.Guild.Id);
        await Service.SetStarCount(ctx.Guild, num).ConfigureAwait(false);
        var count2 = await Service.GetStarCount(ctx.Guild.Id);
        await ctx.Channel.SendConfirmAsync($"Your star count was successfully changed from {count} to {count2}!")
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the star emote for the starboard in the guild.
    /// </summary>
    /// <param name="emote">The star emote. Pass null to view current emote.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task SetStar(IEmote? emote = null)
    {
        if (emote is null)
        {
            var maybeEmote = (await Service.GetStar(ctx.Guild.Id)).ToIEmote();
            if (maybeEmote.Name is null)
            {
                await ctx.Channel.SendErrorAsync(Strings.NoEmoteSet(ctx.Guild.Id), Config);
                return;
            }

            await ctx.Channel.SendConfirmAsync(
                    $"Your current starboard emote is {maybeEmote} {Format.Code(maybeEmote.ToString())}")
                .ConfigureAwait(false);
            return;
        }

        try
        {
            await ctx.Message.AddReactionAsync(emote).ConfigureAwait(false);
        }
        catch
        {
            await ctx.Channel.SendErrorAsync(Strings.InvalidEmote(ctx.Guild.Id), Config)
                .ConfigureAwait(false);
            return;
        }

        await Service.SetStar(ctx.Guild, emote.ToString()).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync($"Successfully set the star to {emote}").ConfigureAwait(false);
    }

    /// <summary>
    ///     Toggles whether a channel is checked for starboard posts.
    /// </summary>
    /// <param name="channel">The channel to toggle.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task StarboardChToggle([Remainder] ITextChannel channel)
    {
        if (!await Service.ToggleChannel(ctx.Guild, channel.Id.ToString()).ConfigureAwait(false))
        {
            await ctx.Channel.SendConfirmAsync(
                    $"{channel.Mention} has been added to the whitelist/blacklist (Depnding on what was set in {await guildSettings.GetPrefix(ctx.Guild)}swm)")
                .ConfigureAwait(false);
        }
        else
        {
            await ctx.Channel.SendConfirmAsync(
                    $"{channel.Mention} has been removed from the whitelist/blacklist (Depending on what was set in {await guildSettings.GetPrefix(ctx.Guild)}swm)")
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Sets the whitelist/blacklist mode for starboard posts in the guild.
    /// </summary>
    /// <param name="mode">The whitelist/blacklist mode.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task StarboardWlMode(WhitelistMode mode)
    {
        if (mode > 0)
        {
            await Service.SetCheckMode(ctx.Guild, true).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.StarboardBlacklistEnabled(ctx.Guild.Id));
        }
        else
        {
            await Service.SetCheckMode(ctx.Guild, false).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.StarboardWhitelistEnabled(ctx.Guild.Id));
        }
    }

    /// <summary>
    ///     Sets whether to remove starboard posts when reactions are cleared from the original message.
    /// </summary>
    /// <param name="enabled">Whether to remove starboard posts on reactions cleared.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task StarboardRemoveOnReactionsCleared(bool enabled)
    {
        await Service.SetRemoveOnClear(ctx.Guild, enabled).ConfigureAwait(false);
        if (enabled)
            await ctx.Channel.SendConfirmAsync(Strings.StarboardReactionsClearRemoveEnabled(ctx.Guild.Id))
                .ConfigureAwait(false);
        else
            await ctx.Channel.SendConfirmAsync(Strings.StarboardReactionsClearRemoveDisabled(ctx.Guild.Id))
                .ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets whether to remove starboard posts when the original message is deleted.
    /// </summary>
    /// <param name="enabled">Whether to remove starboard posts on original message deletion.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task StarboardRemoveOnDelete(bool enabled)
    {
        await Service.SetRemoveOnDelete(ctx.Guild, enabled).ConfigureAwait(false);
        if (enabled)
            await ctx.Channel.SendConfirmAsync(Strings.StarboardDeleteRemoveEnabled(ctx.Guild.Id))
                .ConfigureAwait(false);
        else
            await ctx.Channel.SendConfirmAsync(Strings.StarboardDeleteRemoveDisabled(ctx.Guild.Id))
                .ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets whether to remove starboard posts when the star count falls below the threshold.
    /// </summary>
    /// <param name="enabled">Whether to remove starboard posts when star count falls below the threshold.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task StarboardRemoveOnBelowThreshold(bool enabled)
    {
        await Service.SetRemoveOnBelowThreshold(ctx.Guild, enabled).ConfigureAwait(false);
        if (enabled)
            await ctx.Channel
                .SendConfirmAsync(
                    "Starboard posts will now be removed when the messages star count is below the current star count.")
                .ConfigureAwait(false);
        else
            await ctx.Channel
                .SendConfirmAsync(
                    "Starboard posts will no longer be removed when the messages star count is below the current star count")
                .ConfigureAwait(false);
    }


    /// <summary>
    ///     Sets whether to allow bots to be counted for starboard posts in the guild.
    /// </summary>
    /// <param name="enabled">Whether to allow bots to be counted.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task StarboardAllowBots(bool enabled)
    {
        await Service.SetStarboardAllowBots(ctx.Guild, enabled).ConfigureAwait(false);
        if (enabled)
            await ctx.Channel.SendConfirmAsync(Strings.StarboardBotsEnabled(ctx.Guild.Id));
        else
            await ctx.Channel.SendConfirmAsync(Strings.StarboardBotsDisabled(ctx.Guild.Id));
    }
}
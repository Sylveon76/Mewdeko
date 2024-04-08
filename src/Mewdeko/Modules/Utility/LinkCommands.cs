﻿using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

/// <summary>
/// Commands for managing link previews.
/// </summary>
public class LinkCommands : MewdekoSubmodule<UtilityService>
{
    /// <summary>
    /// Toggles link previews on or off for the server.
    /// </summary>
    /// <param name="yesnt">"yes" to enable link previews, literally anything else to disable.</param>
    [Cmd, Aliases, UserPerm(GuildPermission.Administrator),
     RequireContext(ContextType.Guild)]
    public async Task PreviewLinks(string yesnt)
    {
        await Service.PreviewLinks(ctx.Guild, yesnt[..1].ToLower()).ConfigureAwait(false);
        switch (await Service.GetPLinks(ctx.Guild.Id))
        {
            case 1:
                await ctx.Channel.SendConfirmAsync("Link  previews are now enabled!").ConfigureAwait(false);
                break;
            case 0:
                await ctx.Channel.SendConfirmAsync("Link Previews are now disabled!").ConfigureAwait(false);
                break;
        }
    }
}
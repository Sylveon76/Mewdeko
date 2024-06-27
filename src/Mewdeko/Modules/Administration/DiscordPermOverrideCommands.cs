﻿using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    /// <summary>
    /// Module for managing Discord permission overrides. This module allows administrators to override the required permissions for a command.
    /// </summary>
    /// <param name="serv"></param>
    [Group]
    public class DiscordPermOverrideCommands(InteractiveService serv) : MewdekoSubmodule<DiscordPermOverrideService>
    {
        /// <summary>
        /// Overrides the required permissions for a specific command in the current guild.
        /// </summary>
        /// <param name="cmd">The command for which the permissions will be overridden</param>
        /// <param name="perms">The permissions required to execute the command</param>
        /// <remarks>
        /// If no permissions are provided, the override for the command will be removed.
        /// This command requires the caller to have GuildPermission.Administrator.
        /// </remarks>
        /// <example>.discordpermoverride CommandName Permission1 Permission2 ...</example>
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task DiscordPermOverride(CommandOrCrInfo cmd, params GuildPermission[]? perms)
        {
            if (perms is null || perms.Length == 0)
            {
                await Service.RemoveOverride(ctx.Guild.Id, cmd.Name).ConfigureAwait(false);
                await ReplyConfirmLocalizedAsync("perm_override_reset").ConfigureAwait(false);
                return;
            }

            var aggregatePerms = perms.Aggregate((acc, seed) => seed | acc);
            await Service.AddOverride(Context.Guild.Id, cmd.Name, aggregatePerms).ConfigureAwait(false);

            await ReplyConfirmLocalizedAsync("perm_override",
                Format.Bold(aggregatePerms.ToString()),
                Format.Code(cmd.Name)).ConfigureAwait(false);
        }


        /// <summary>
        /// Resets all command permission overrides in the current guild.
        /// </summary>
        /// <remarks>
        /// This command requires the caller to have GuildPermission.Administrator.
        /// </remarks>
        /// <example>.discordpermoverridereset</example>
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task DiscordPermOverrideReset()
        {
            var result = await PromptUserConfirmAsync(new EmbedBuilder()
                .WithOkColor()
                .WithDescription(GetText("perm_override_all_confirm")), ctx.User.Id).ConfigureAwait(false);

            if (!result)
                return;
            await Service.ClearAllOverrides(Context.Guild.Id).ConfigureAwait(false);

            await ReplyConfirmLocalizedAsync("perm_override_all").ConfigureAwait(false);
        }

        /// <summary>
        /// Lists all command permission overrides in the current guild.
        /// </summary>
        /// <remarks>
        /// This command requires the caller to have GuildPermission.Administrator.
        /// </remarks>
        /// <example>.discordpermoverridelist</example>
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task DiscordPermOverrideList()
        {
            var overrides = await Service.GetAllOverrides(Context.Guild.Id);
            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(overrides.Count() / 9)
                .WithDefaultCanceledPage()
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();
            await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                var thisPageOverrides = overrides
                    .Skip(9 * page)
                    .Take(9)
                    .ToList();
                if (thisPageOverrides.Count == 0)
                {
                    return new PageBuilder().WithDescription(GetText("perm_override_page_none"))
                        .WithColor(Mewdeko.ErrorColor);
                }

                return new PageBuilder()
                    .WithDescription(string.Join("\n",
                        thisPageOverrides.Select(ov => $"{ov.Command} => {ov.Perm.ToString()}")))
                    .WithColor(Mewdeko.OkColor);
            }
        }
    }
}
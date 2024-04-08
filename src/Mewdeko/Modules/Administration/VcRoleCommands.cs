﻿using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    /// <summary>
    /// Module for managing voice channel roles.
    /// </summary>
    [Group]
    public class VcRoleCommands : MewdekoSubmodule<VcRoleService>
    {
        /// <summary>
        /// Unbinds a role from a voice channel.
        /// </summary>
        /// <param name="vcId">The voice channel id</param>
        [Cmd, Aliases, UserPerm(GuildPermission.ManageRoles),
         BotPerm(GuildPermission.ManageRoles), RequireContext(ContextType.Guild)]
        public async Task VcRoleRm(ulong vcId)
        {
            if (await Service.RemoveVcRole(ctx.Guild.Id, vcId))
            {
                await ReplyConfirmLocalizedAsync("vcrole_removed", Format.Bold(vcId.ToString()))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorLocalizedAsync("vcrole_not_found").ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Adds or removes a role from a voice channel.
        /// </summary>
        /// <param name="vchan">The channel you want to manage a role for</param>
        /// <param name="role">The role you want to set for that voice channel (optional)</param>
        [Cmd, Aliases, UserPerm(GuildPermission.ManageRoles),
         BotPerm(GuildPermission.ManageRoles), RequireContext(ContextType.Guild)]
        public async Task VcRole(SocketGuildChannel vchan, [Remainder] IRole? role = null)
        {
            if (vchan is IVoiceChannel chan)
            {
                if (role == null)
                {
                    if (await Service.RemoveVcRole(ctx.Guild.Id, chan.Id))
                    {
                        await ReplyConfirmLocalizedAsync("vcrole_removed", Format.Bold(chan.Name))
                            .ConfigureAwait(false);
                    }
                }
                else
                {
                    await Service.AddVcRole(ctx.Guild.Id, role, chan.Id);
                    await ReplyConfirmLocalizedAsync("vcrole_added", Format.Bold(chan.Name), Format.Bold(role.Name))
                        .ConfigureAwait(false);
                }
            }
            else
            {
                await ctx.Channel.SendErrorAsync("This is not a voice channel!", Config).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Binds or unbinds a role from a voice channel while in a voice channel.
        /// </summary>
        /// <param name="role">The role to bind to the voice channel.</param>
        [Cmd, Aliases, UserPerm(GuildPermission.ManageRoles),
         BotPerm(GuildPermission.ManageRoles), RequireContext(ContextType.Guild)]
        public async Task VcRole([Remainder] IRole? role = null)
        {
            var user = (IGuildUser)ctx.User;

            var vc = user.VoiceChannel;

            if (vc == null || vc.GuildId != user.GuildId)
            {
                await ReplyErrorLocalizedAsync("must_be_in_voice").ConfigureAwait(false);
                return;
            }

            if (role == null)
            {
                if (await Service.RemoveVcRole(ctx.Guild.Id, vc.Id))
                    await ReplyConfirmLocalizedAsync("vcrole_removed", Format.Bold(vc.Name)).ConfigureAwait(false);
            }
            else
            {
                await Service.AddVcRole(ctx.Guild.Id, role, vc.Id);
                await ReplyConfirmLocalizedAsync("vcrole_added", Format.Bold(vc.Name), Format.Bold(role.Name))
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// List all voice channel roles for this guild.
        /// </summary>
        /// <example>.vcrolelist</example>
        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task VcRoleList()
        {
            var guild = (SocketGuild)ctx.Guild;
            string? text;
            if (Service.VcRoles.TryGetValue(ctx.Guild.Id, out var roles))
            {
                if (roles.Count == 0)
                {
                    text = GetText("no_vcroles");
                }
                else
                {
                    text = string.Join("\n", roles.Select(x =>
                        $"{Format.Bold(guild.GetVoiceChannel(x.Key)?.Name ?? x.Key.ToString())} => {x.Value}"));
                }
            }
            else
            {
                text = GetText("no_vcroles");
            }

            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithTitle(GetText("vc_role_list"))
                    .WithDescription(text))
                .ConfigureAwait(false);
        }
    }
}
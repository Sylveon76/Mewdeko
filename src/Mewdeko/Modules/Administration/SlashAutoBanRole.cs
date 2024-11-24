using Discord.Interactions;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

public partial class SlashAdministration

{
    /// <summary>
    ///     Slash command group for managing the AutoBanRole feature.
    /// </summary>
    [Group("autobanrole", "Allows you to set or remove a role from autobanning a user when they add it.")]
    public class SlashAutoBanRole : MewdekoSlashSubmodule<AutoBanRoleService>
    {
        /// <summary>
        ///     Adds a role to the list of AutoBanRoles.
        /// </summary>
        /// <param name="role">The role to add</param>
        [SlashCommand("add", "Add a role to the list of AutoBanRoles")]
        public async Task AutoBanRoleAdd(IRole role)
        {
            var success = await Service.AddAutoBanRole(Context.Guild.Id, role.Id);
            if (success)
            {
                await ReplyConfirmAsync(Strings.AbroleAdd(ctx.Guild.Id, role.Mention)).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.AbroleExists(ctx.Guild.Id, role.Mention)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Removes a role from the list of AutoBanRoles.
        /// </summary>
        /// <param name="role">The role to remove</param>
        [SlashCommand("remove", "Remove a role from the list of AutoBanRoles")]
        public async Task AutoBanRoleRemove(IRole role)
        {
            var success = await Service.RemoveAutoBanRole(Context.Guild.Id, role.Id);
            if (success)
            {
                await ReplyConfirmAsync(Strings.AbroleRemove(ctx.Guild.Id, role.Mention)).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.AbroleNotexists(ctx.Guild.Id, role.Mention)).ConfigureAwait(false);
            }
        }
    }
}
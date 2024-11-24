using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Modules.Permissions.Services;

namespace Mewdeko.Modules.Permissions;

public partial class Permissions
{
    /// <summary>
    ///     Provides commands for managing global permissions, allowing for the blocking or unblocking of specific commands and
    ///     modules across all guilds.
    /// </summary>
    [Group]
    [OwnerOnly]
    public class GlobalPermissionCommands : MewdekoSubmodule<GlobalPermissionService>
    {
        /// <summary>
        ///     Lists all currently globally blocked modules and commands.
        /// </summary>
        /// <returns>A task representing the asynchronous operation to send the list of globally blocked modules and commands.</returns>
        /// <remarks>
        ///     This command is restricted to bot owners. It provides an overview of all modules and commands that have been
        ///     globally restricted.
        /// </remarks>
        [Cmd]
        [Aliases]
        public async Task GlobalPermList()
        {
            var blockedModule = Service.BlockedModules;
            var blockedCommands = Service.BlockedCommands;
            if (blockedModule.Count == 0 && blockedCommands.Count == 0)
            {
                await ReplyErrorAsync(Strings.LgpNone(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder().WithOkColor();

            if (blockedModule.Count > 0)
            {
                embed.AddField(efb => efb
                    .WithName(Strings.BlockedModules(ctx.Guild.Id))
                    .WithValue(string.Join("\n", Service.BlockedModules))
                    .WithIsInline(false));
            }

            if (blockedCommands.Count > 0)
            {
                embed.AddField(efb => efb
                    .WithName(Strings.BlockedCommands(ctx.Guild.Id))
                    .WithValue(string.Join("\n", Service.BlockedCommands))
                    .WithIsInline(false));
            }

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        /// <summary>
        ///     Resets all global permissions, clearing all global command and module blocks.
        /// </summary>
        /// <returns>A task representing the asynchronous operation to reset global permissions.</returns>
        /// <remarks>
        ///     This command is restricted to bot owners. Use this command with caution as it will remove all global restrictions.
        /// </remarks>
        [Cmd]
        [Aliases]
        public async Task ResetGlobalPerms()
        {
            await Service.Reset().ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.GlobalPermsReset(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles a module on or off the global block list.
        /// </summary>
        /// <param name="module">The module to toggle.</param>
        /// <returns>A task representing the asynchronous operation to block or unblock the module globally.</returns>
        /// <remarks>
        ///     This command is restricted to bot owners. It allows for specifying modules to be globally blocked or unblocked.
        /// </remarks>
        [Cmd]
        [Aliases]
        public async Task GlobalModule(ModuleOrCrInfo module)
        {
            var moduleName = module.Name.ToLowerInvariant();

            var added = Service.ToggleModule(moduleName);

            if (added)
            {
                await ReplyConfirmAsync(Strings.GmodAdd(ctx.Guild.Id, Format.Bold(module.Name))).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(Strings.GmodRemove(ctx.Guild.Id, Format.Bold(module.Name))).ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles a command on or off the global block list.
        /// </summary>
        /// <param name="cmd">The command to toggle.</param>
        /// <returns>A task representing the asynchronous operation to block or unblock the command globally.</returns>
        /// <remarks>
        ///     This command is restricted to bot owners. Certain commands, like "source", are protected from being globally
        ///     disabled.
        /// </remarks>
        [Cmd]
        [Aliases]
        public async Task GlobalCommand(CommandOrCrInfo cmd)
        {
            var commandName = cmd.Name.ToLowerInvariant();
            if (commandName is "source")
            {
                await ctx.Channel
                    .SendErrorAsync(Strings.CommandProtected(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            var added = Service.ToggleCommand(commandName);

            if (added)
            {
                await ReplyConfirmAsync(Strings.GcmdAdd(ctx.Guild.Id, Format.Bold(cmd.Name))).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(Strings.GcmdRemove(ctx.Guild.Id, Format.Bold(cmd.Name))).ConfigureAwait(false);
        }
    }
}
using Discord.Commands;
using LinqToDB;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Server_Management.Services;

namespace Mewdeko.Modules.Server_Management;

public partial class ServerManagement
{
    /// <summary>
    ///     Manages role monitoring settings, blacklists, and whitelists.
    /// </summary>
    public class RoleMonitorCommands(DbContextProvider dbContextProvider) : MewdekoSubmodule<RoleMonitorService>
    {
        /// <summary>
        ///     Sets the default punishment action for the guild.
        /// </summary>
        /// <param name="punishmentAction">The default punishment action to set.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireServerOwner]
        public async Task SetDefaultPunishment(PunishmentAction punishmentAction)
        {
            await Service.SetDefaultPunishmentAsync(ctx.Guild, punishmentAction);
            await SuccessAsync(Strings.DefaultPunishmentSet(ctx.Guild.Id, punishmentAction)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Adds a role to the blacklist.
        /// </summary>
        /// <param name="role">The role to blacklist.</param>
        /// <param name="punishmentAction">Optional punishment action specific to this role.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireServerOwner]
        public async Task AddBlacklistedRole(IRole role, PunishmentAction? punishmentAction = null)
        {
            try
            {
                await Service.AddBlacklistedRoleAsync(ctx.Guild, role, punishmentAction);
                await SuccessAsync(Strings.RoleBlacklisted(ctx.Guild.Id, role.Name)).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                await ErrorAsync(Strings.RoleBlacklistError(ctx.Guild.Id, ex.Message, role.Mention)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Removes a role from the blacklist.
        /// </summary>
        /// <param name="role">The role to remove from the blacklist.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireServerOwner]
        public async Task RemoveBlacklistedRole(IRole role)
        {
            try
            {
                await Service.RemoveBlacklistedRoleAsync(ctx.Guild, role);
                await SuccessAsync(Strings.RoleUnblacklisted(ctx.Guild.Id, role.Name)).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                await ErrorAsync(Strings.RoleUnblacklistError(ctx.Guild.Id, ex.Message, role.Mention)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Adds a permission to the blacklist.
        /// </summary>
        /// <param name="permission">The permission to blacklist.</param>
        /// <param name="punishmentAction">Optional punishment action specific to this permission.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireServerOwner]
        public async Task AddBlacklistedPermission(GuildPermission permission,
            PunishmentAction? punishmentAction = null)
        {
            try
            {
                await Service.AddBlacklistedPermissionAsync(ctx.Guild, permission, punishmentAction);
                await SuccessAsync(Strings.PermissionBlacklisted(ctx.Guild.Id, permission)).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                await ErrorAsync(Strings.PermissionBlacklistError(ctx.Guild.Id, ex.Message, permission)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Removes a permission from the blacklist.
        /// </summary>
        /// <param name="permission">The permission to remove from the blacklist.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireServerOwner]
        public async Task RemoveBlacklistedPermission(GuildPermission permission)
        {
            try
            {
                await Service.RemoveBlacklistedPermissionAsync(ctx.Guild, permission);
                await SuccessAsync(Strings.PermissionUnblacklisted(ctx.Guild.Id, permission)).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                await ErrorAsync(Strings.PermissionUnblacklistError(ctx.Guild.Id, ex.Message, permission)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Adds a user to the whitelist.
        /// </summary>
        /// <param name="user">The user to whitelist.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireServerOwner]
        public async Task AddWhitelistedUser(IGuildUser user)
        {
            await Service.AddWhitelistedUserAsync(ctx.Guild, user);
            await SuccessAsync(Strings.UserWhitelisted(ctx.Guild.Id, user.Username)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Removes a user from the whitelist.
        /// </summary>
        /// <param name="user">The user to remove from the whitelist.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireServerOwner]
        public async Task RemoveWhitelistedUser(IGuildUser user)
        {
            await Service.RemoveWhitelistedUserAsync(ctx.Guild, user);
            await SuccessAsync(Strings.UserUnwhitelisted(ctx.Guild.Id, user.Username)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Adds a role to the whitelist.
        /// </summary>
        /// <param name="role">The role to whitelist.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireServerOwner]
        public async Task AddWhitelistedRole(IRole role)
        {
            await Service.AddWhitelistedRoleAsync(ctx.Guild, role);
            await SuccessAsync(Strings.RoleWhitelisted(ctx.Guild.Id, role.Name)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Removes a role from the whitelist.
        /// </summary>
        /// <param name="role">The role to remove from the whitelist.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireServerOwner]
        public async Task RemoveWhitelistedRole(IRole role)
        {
            await Service.RemoveWhitelistedRoleAsync(ctx.Guild, role);
            await SuccessAsync(Strings.RoleUnwhitelisted(ctx.Guild.Id, role.Name)).ConfigureAwait(false);        }

        /// <summary>
        ///     Lists all blacklisted and whitelisted roles and permissions.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireServerOwner]
        public async Task ListBlacklists()
        {
            await using var context = await dbContextProvider.GetContextAsync();

            var blacklistedRoles = await context.BlacklistedRoles
                .Where(r => r.GuildId == ctx.Guild.Id)
                .ToListAsync();

            var blacklistedPermissions = await context.BlacklistedPermissions
                .Where(p => p.GuildId == ctx.Guild.Id)
                .ToListAsync();

            var whitelistedUsers = await context.WhitelistedUsers
                .Where(u => u.GuildId == ctx.Guild.Id)
                .ToListAsync();

            var whitelistedRoles = await context.WhitelistedRoles
                .Where(r => r.GuildId == ctx.Guild.Id)
                .ToListAsync();

            var embed = new EmbedBuilder()
                .WithTitle(Strings.BlacklistWhitelistListTitle(ctx.Guild.Id))
                .WithColor(Color.Red);

            if (blacklistedRoles.Any())
            {
                var roles = blacklistedRoles.Select(r =>
                {
                    var role = ctx.Guild.GetRole(r.RoleId);
                    var roleName = role != null ? role.Name : Strings.RoleIdFormat(ctx.Guild.Id, r.RoleId);
                    var punishment = r.PunishmentAction?.ToString() ?? Strings.Default(ctx.Guild.Id);
                    return $"{roleName} - {Strings.PunishmentFormat(ctx.Guild.Id, punishment)}";
                });

                embed.AddField(Strings.BlacklistedRoles(ctx.Guild.Id), string.Join("\n", roles));
            }
            else
            {
                embed.AddField(Strings.BlacklistedRoles(ctx.Guild.Id), Strings.NoBlacklistedRoles(ctx.Guild.Id));
            }

            if (blacklistedPermissions.Any())
            {
                var permissions = blacklistedPermissions.Select(p =>
                {
                    var punishment = p.PunishmentAction?.ToString() ?? Strings.Default(ctx.Guild.Id);
                    return $"{p.Permission} - {Strings.PunishmentFormat(ctx.Guild.Id, punishment)}";
                });

                embed.AddField(Strings.BlacklistedPermissions(ctx.Guild.Id), string.Join("\n", permissions));
            }
            else
            {
                embed.AddField(Strings.BlacklistedPermissions(ctx.Guild.Id), Strings.NoBlacklistedPermissions(ctx.Guild.Id));
            }

            if (whitelistedRoles.Any())
            {
                var roles = whitelistedRoles.Select(r =>
                {
                    var role = ctx.Guild.GetRole(r.RoleId);
                    var roleName = role != null ? role.Name : Strings.RoleIdFormat(ctx.Guild.Id, r.RoleId);
                    return $"{roleName}";
                });

                embed.AddField(Strings.WhitelistedRoles(ctx.Guild.Id), string.Join("\n", roles));
            }
            else
            {
                embed.AddField(Strings.WhitelistedRoles(ctx.Guild.Id), Strings.NoWhitelistedRoles(ctx.Guild.Id));
            }

            if (whitelistedUsers.Count != 0)
            {
                var users = whitelistedUsers.Select(async u =>
                {
                    var user = await ctx.Guild.GetUserAsync(u.UserId);
                    var userName = user != null ? user.Username : Strings.UserIdFormat(ctx.Guild.Id, u.UserId);
                    return $"{userName}";
                });

                embed.AddField(Strings.WhitelistedUsers(ctx.Guild.Id), string.Join("\n", users));
            }
            else
            {
                embed.AddField(Strings.WhitelistedUsers(ctx.Guild.Id), Strings.NoWhitelistedUsers(ctx.Guild.Id));
            }

            await ctx.Channel.SendMessageAsync(embed: embed.Build()).ConfigureAwait(false);
        }
    }
}
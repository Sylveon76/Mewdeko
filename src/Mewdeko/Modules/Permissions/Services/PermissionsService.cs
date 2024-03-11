﻿using Discord.Commands;
using Discord.Interactions;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Services.strings;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Permissions.Services;

public class PermissionService : ILateBlocker, INService
{
    private readonly DbService db;
    public readonly IBotStrings Strings;
    private readonly GuildSettingsService guildSettings;

    public PermissionService(DbService db,
        IBotStrings strings,
        GuildSettingsService guildSettings, Mewdeko bot)
    {
        this.db = db;
        Strings = strings;
        this.guildSettings = guildSettings;
        var allgc = bot.AllGuildConfigs;
        using var uow = this.db.GetDbContext();
        foreach (var x in allgc)
        {
            Cache.TryAdd(x.GuildId,
                new PermissionCache
                {
                    Verbose = false.ParseBoth(x.VerbosePermissions.ToString()),
                    PermRole = x.PermissionRole,
                    Permissions = new PermissionsCollection<Permissionv2>(x.Permissions)
                });
        }
    }

    //guildid, root permission
    public ConcurrentDictionary<ulong, PermissionCache> Cache { get; } = new();

    public int Priority { get; } = 0;

    public async Task<bool> TryBlockLate(
        DiscordSocketClient client,
        ICommandContext ctx,
        string moduleName,
        CommandInfo command)
    {
        var guild = ctx.Guild;
        var msg = ctx.Message;
        var user = ctx.User;
        var channel = ctx.Channel;
        var commandName = command.Name.ToLowerInvariant();

        await Task.Yield();
        if (guild == null)
            return false;

        var resetCommand = commandName == "resetperms";

        var pc = await GetCacheFor(guild.Id);
        if (!resetCommand && !pc.Permissions.CheckPermissions(msg, commandName, moduleName, out var index))
        {
            if (pc.Verbose)
            {
                try
                {
                    await channel.SendErrorAsync(Strings.GetText("perm_prevent", guild.Id, index + 1,
                            Format.Bold(pc.Permissions[index]
                                .GetCommand(await guildSettings.GetPrefix(guild), (SocketGuild)guild))))
                        .ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            }

            return true;
        }

        if (moduleName == nameof(Permissions))
        {
            if (user is not IGuildUser guildUser)
                return true;

            if (guildUser.GuildPermissions.Administrator)
                return false;

            var permRole = pc.PermRole;
            if (!ulong.TryParse(permRole, out var rid))
                rid = 0;
            string? returnMsg;
            IRole role;
            if (string.IsNullOrWhiteSpace(permRole) || (role = guild.GetRole(rid)) == null)
            {
                returnMsg = "You need Admin permissions in order to use permission commands.";
                if (pc.Verbose)
                {
                    try
                    {
                        await channel.SendErrorAsync(returnMsg).ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                }

                return true;
            }

            if (!guildUser.RoleIds.Contains(rid))
            {
                returnMsg = $"You need the {Format.Bold(role.Name)} role in order to use permission commands.";
                if (pc.Verbose)
                {
                    try
                    {
                        await channel.SendErrorAsync(returnMsg).ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                }

                return true;
            }

            return false;
        }

        return false;
    }

    public async Task<bool> TryBlockLate(DiscordSocketClient client, IInteractionContext ctx, ICommandInfo command)
    {
        var guild = ctx.Guild;
        var commandName = command.MethodName.ToLowerInvariant();

        await Task.Yield();
        if (guild == null)
            return false;

        var resetCommand = commandName == "resetperms";

        var pc = await GetCacheFor(guild.Id);
        if (resetCommand || pc.Permissions.CheckSlashPermissions(command.Module.SlashGroupName, commandName, ctx.User,
                ctx.Channel, out var index))
            return false;
        try
        {
            await ctx.Interaction.SendEphemeralErrorAsync(Strings.GetText("perm_prevent", guild.Id, index + 1,
                    Format.Bold(pc.Permissions[index]
                        .GetCommand(await guildSettings.GetPrefix(guild), (SocketGuild)guild))))
                .ConfigureAwait(false);
        }
        catch
        {
            // ignored
        }

        return true;
    }

    public async Task<PermissionCache?> GetCacheFor(ulong guildId)
    {
        if (Cache.TryGetValue(guildId, out var pc))
            return pc;
        await using (var uow = db.GetDbContext())
        {
            var config = await uow.ForGuildId(guildId,
                set => set.Include(x => x.Permissions));
            UpdateCache(config);
        }

        Cache.TryGetValue(guildId, out pc);
        return pc ?? null;
    }

    public async Task AddPermissions(ulong guildId, params Permissionv2[] perms)
    {
        await using var uow = db.GetDbContext();
        var config = await uow.GcWithPermissionsv2For(guildId);
        //var orderedPerms = new PermissionsCollection<Permissionv2>(config.Permissions);
        var max = config.Permissions.Max(x => x.Index); //have to set its index to be the highest
        foreach (var perm in perms)
        {
            perm.Index = ++max;
            config.Permissions.Add(perm);
        }

        await uow.SaveChangesAsync().ConfigureAwait(false);
        UpdateCache(config);
    }

    public void UpdateCache(GuildConfig config) =>
        Cache.AddOrUpdate(config.GuildId, new PermissionCache
        {
            Permissions = new PermissionsCollection<Permissionv2>(config.Permissions),
            PermRole = config.PermissionRole,
            Verbose = false.ParseBoth(config.VerbosePermissions.ToString())
        }, (_, old) =>
        {
            old.Permissions = new PermissionsCollection<Permissionv2>(config.Permissions);
            old.PermRole = config.PermissionRole;
            old.Verbose = false.ParseBoth(config.VerbosePermissions.ToString());
            return old;
        });

    public async Task Reset(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var config = await uow.GcWithPermissionsv2For(guildId);
        config.Permissions = Permissionv2.GetDefaultPermlist;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        UpdateCache(config);
    }

    public static string MentionPerm(PrimaryPermissionType t, ulong id)
        => t switch
        {
            PrimaryPermissionType.User => $"<@{id}>",
            PrimaryPermissionType.Channel => $"<#{id}>",
            PrimaryPermissionType.Role => $"<@&{id}>",
            PrimaryPermissionType.Server => "This Server",
            PrimaryPermissionType.Category => $"<#{id}>",
            _ =>
                "An unexpected type input error occurred in `PermissionsService.cs#MentionPerm(PrimaryPermissionType, ulong)`. Please contact a developer at https://discord.gg/mewdeko with a screenshot of this message for more information."
        };

    public async Task RemovePerm(ulong guildId, int index)
    {
        await using var uow = db.GetDbContext();

        var config = await uow.GcWithPermissionsv2For(guildId);
        var permsCol = new PermissionsCollection<Permissionv2>(config.Permissions);

        var p = permsCol[index];
        permsCol.RemoveAt(index);
        uow.Remove(p);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        UpdateCache(config);
    }

    public async Task UpdatePerm(ulong guildId, int index, bool state)
    {
        await using var uow = db.GetDbContext();

        var config = await uow.GcWithPermissionsv2For(guildId);
        var permsCol = new PermissionsCollection<Permissionv2>(config.Permissions);

        var p = permsCol[index];
        p.State = state ? 1 : 0;
        uow.Update(p);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        UpdateCache(config);
    }

    public async Task UnsafeMovePerm(ulong guildId, int from, int to)
    {
        await using var uow = db.GetDbContext();

        var config = await uow.GcWithPermissionsv2For(guildId);
        var permsCol = new PermissionsCollection<Permissionv2>(config.Permissions);

        var fromFound = from < permsCol.Count;
        var toFound = to < permsCol.Count;

        if (!fromFound || !toFound)
        {
            return;
        }

        var fromPerm = permsCol[from];

        permsCol.RemoveAt(from);
        permsCol.Insert(to, fromPerm);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        UpdateCache(config);
    }
}
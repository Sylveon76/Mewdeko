﻿using System.Collections.Generic;
using Discord;
using Discord.WebSocket;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Common.PubSub;
using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Permissions.Services;

public sealed class BlacklistService : IEarlyBehavior, INService
{
    private readonly DbService _db;
    private readonly IPubSub _pubSub;

    private readonly TypedKey<BlacklistEntry[]> _blPubKey = new("blacklist.reload");
    private IReadOnlyList<BlacklistEntry> blacklist;

    public BlacklistService(DbService db, IPubSub pubSub)
    {
        _db = db;
        _pubSub = pubSub;

        Reload(false);
        _pubSub.Sub(_blPubKey, OnReload);
    }

    public int Priority => -100;

    public ModuleBehaviorType BehaviorType => ModuleBehaviorType.Blocker;

    public Task<bool> RunBehavior(DiscordSocketClient _, IGuild guild, IUserMessage usrMsg)
    {
        foreach (var bl in blacklist)
        {
            if (guild != null && bl.Type == BlacklistType.Server && bl.ItemId == guild.Id)
                return Task.FromResult(true);

            if (bl.Type == BlacklistType.Channel && bl.ItemId == usrMsg.Channel.Id)
                return Task.FromResult(true);

            if (bl.Type == BlacklistType.User && bl.ItemId == usrMsg.Author.Id)
                return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
    public Task<bool> RunBehavior(DiscordSocketClient _, IGuild guild, IUser user, IMessageChannel channel)
    {
        foreach (var bl in blacklist)
        {
            if (guild != null && bl.Type == BlacklistType.Server && bl.ItemId == guild.Id)
                return Task.FromResult(true);

            if (bl.Type == BlacklistType.Channel && bl.ItemId == channel.Id)
                return Task.FromResult(true);

            if (bl.Type == BlacklistType.User && bl.ItemId == user.Id)
                return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    private ValueTask OnReload(BlacklistEntry[] blacklist)
    {
        this.blacklist = blacklist;
        return default;
    }

    public void Reload(bool publish = true)
    {
        using var uow = _db.GetDbContext();
        var toPublish = uow.Context.Blacklist.AsNoTracking().ToArray();
        blacklist = toPublish;
        if (publish) _pubSub.Pub(_blPubKey, toPublish);
    }

    public void Blacklist(BlacklistType type, ulong id)
    {
        using var uow = _db.GetDbContext();
        var item = new BlacklistEntry {ItemId = id, Type = type};
        uow.Context.Blacklist.Add(item);
        uow.SaveChanges();

        Reload();
    }

    public void UnBlacklist(BlacklistType type, ulong id)
    {
        using var uow = _db.GetDbContext();
        var toRemove = uow.Context.Blacklist
            .FirstOrDefault(bi => bi.ItemId == id && bi.Type == type);

        if (toRemove is not null)
            uow.Context.Blacklist.Remove(toRemove);

        uow.SaveChanges();

        Reload();
    }

    public void BlacklistUsers(IReadOnlyCollection<ulong> toBlacklist)
    {
        using (var uow = _db.GetDbContext())
        {
            var bc = uow.Context.Blacklist;
            //blacklist the users
            bc.AddRange(toBlacklist.Select(x =>
                new BlacklistEntry
                {
                    ItemId = x,
                    Type = BlacklistType.User
                }));

            //clear their currencies
            uow.DiscordUsers.RemoveFromMany(toBlacklist);
            uow.SaveChanges();
        }

        Reload();
    }
}
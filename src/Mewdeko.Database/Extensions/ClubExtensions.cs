﻿using LinqToDB.EntityFrameworkCore;
using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class ClubExtensions
{
    private static IQueryable<ClubInfo> Include(this IQueryable<ClubInfo> clubs)
        => clubs.Include(x => x.Owner)
            .Include(x => x.Applicants)
            .ThenInclude(x => x.User)
            .Include(x => x.Bans)
            .ThenInclude(x => x.User)
            .Include(x => x.Users)
            .AsQueryable();

    public static Task<ClubInfo> GetByOwner(this DbSet<ClubInfo> clubs, ulong userId)
        => Include(clubs).FirstOrDefaultAsync(c => c.Owner.UserId == userId);

    public static Task<ClubInfo> GetByOwnerOrAdmin(this DbSet<ClubInfo> clubs, ulong userId)
        => Include(clubs).FirstOrDefaultAsync(c => c.Owner.UserId == userId
                                                   || c.Users.Any(u => u.UserId == userId && u.IsClubAdmin == 1));

    public static Task<ClubInfo> GetByMember(this DbSet<ClubInfo> clubs, ulong userId)
        => Include(clubs).FirstOrDefaultAsync(c => c.Users.Any(u => u.UserId == userId));

    public static ClubInfo GetByName(this DbSet<ClubInfo> clubs, string name, int discrim)
        => Include(clubs).FirstOrDefault(c => c.Name.ToUpper() == name.ToUpper() && c.Discrim == discrim);

    public static async Task<int> GetNextDiscrim(this DbSet<ClubInfo> clubs, string name)
        => await Include(clubs)
            .Where(x => x.Name.ToUpper() == name.ToUpper())
            .Select(x => x.Discrim)
            .DefaultIfEmpty()
            .MaxAsync().ConfigureAwait(false) + 1;

    public static Task<List<ClubInfo>> GetClubLeaderboardPage(this DbSet<ClubInfo> clubs, int page) =>
        clubs
            .AsNoTracking()
            .OrderByDescending(x => x.Xp)
            .Skip(page * 9)
            .Take(9)
            .ToListAsyncEF();
}
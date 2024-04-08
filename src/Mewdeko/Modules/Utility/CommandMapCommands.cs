﻿using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Utility.Services;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    /// Commands for managing command aliases.
    /// </summary>
    /// <param name="db">The database service.</param>
    /// <param name="serv">The interactive service.</param>
    [Group]
    public class CommandMapCommands(DbService db, InteractiveService serv) : MewdekoSubmodule<CommandMapService>
    {
        /// <summary>
        /// Clears all command aliases for the guild.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator)]
        public async Task AliasesClear()
        {
            var count = Service.ClearAliases(ctx.Guild.Id);
            await ReplyConfirmLocalizedAsync("aliases_cleared", count).ConfigureAwait(false);
        }

        /// <summary>
        /// Adds a new alias for a command, or removes an existing alias if no mapping is provided.
        /// </summary>
        /// <param name="trigger">The trigger word for the alias.</param>
        /// <param name="mapping">The command to map to the alias. If null, the alias will be removed.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, UserPerm(GuildPermission.Administrator), RequireContext(ContextType.Guild)]
        public async Task Alias(string trigger, [Remainder] string? mapping = null)
        {
            if (string.IsNullOrWhiteSpace(trigger))
                return;

            trigger = trigger.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(mapping))
            {
                if (!Service.AliasMaps.TryGetValue(ctx.Guild.Id, out var maps) ||
                    !maps.TryRemove(trigger, out _))
                {
                    await ReplyErrorLocalizedAsync("alias_remove_fail", Format.Code(trigger)).ConfigureAwait(false);
                    return;
                }

                var uow = db.GetDbContext();
                await using (uow.ConfigureAwait(false))
                {
                    var config = await uow.ForGuildId(ctx.Guild.Id, set => set.Include(x => x.CommandAliases));
                    var tr = config.CommandAliases.FirstOrDefault(x => x.Trigger == trigger);
                    if (tr != null)
                        uow.Set<CommandAlias>().Remove(tr);
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                }

                await ReplyConfirmLocalizedAsync("alias_removed", Format.Code(trigger)).ConfigureAwait(false);
                return;
            }

            Service.AliasMaps.AddOrUpdate(ctx.Guild.Id, _ =>
            {
                using (var uow = db.GetDbContext())
                {
                    var config = uow.ForGuildId(ctx.Guild.Id, set => set.Include(x => x.CommandAliases)).GetAwaiter()
                        .GetResult();
                    config.CommandAliases.Add(new CommandAlias
                    {
                        Mapping = mapping, Trigger = trigger
                    });
                    uow.SaveChanges();
                }

                return new ConcurrentDictionary<string, string>(new Dictionary<string, string>
                {
                    {
                        trigger.Trim().ToLowerInvariant(), mapping.ToLowerInvariant()
                    }
                });
            }, (_, map) =>
            {
                using (var uow = db.GetDbContext())
                {
                    var config = uow.ForGuildId(ctx.Guild.Id, set => set.Include(x => x.CommandAliases)).GetAwaiter()
                        .GetResult();
                    var toAdd = new CommandAlias
                    {
                        Mapping = mapping, Trigger = trigger
                    };
                    var toRemove = config.CommandAliases.Where(x => x.Trigger == trigger);
                    if (toRemove.Any())
                        uow.RemoveRange(toRemove);
                    config.CommandAliases.Add(toAdd);
                    uow.SaveChanges();
                }

                map.AddOrUpdate(trigger, mapping, (_, _) => mapping);
                return map;
            });

            await ReplyConfirmLocalizedAsync("alias_added", Format.Code(trigger), Format.Code(mapping))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Lists all command aliases currently set for the guild.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task AliasList()
        {
            if (!Service.AliasMaps.TryGetValue(ctx.Guild.Id, out var maps) || maps.Count == 0)
            {
                await ReplyErrorLocalizedAsync("aliases_none").ConfigureAwait(false);
                return;
            }

            var arr = maps.ToArray();

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(arr.Length / 10)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                return new PageBuilder().WithOkColor()
                    .WithTitle(GetText("alias_list"))
                    .WithDescription(string.Join("\n",
                        arr.Skip(page * 10).Take(10).Select(x => $"`{x.Key}` => `{x.Value}`")));
            }
        }
    }
}
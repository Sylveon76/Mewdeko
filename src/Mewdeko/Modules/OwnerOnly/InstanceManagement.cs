using Discord.Commands;
using Fergun.Interactive;
using LinqToDB.EntityFrameworkCore;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.OwnerOnly.Services;

namespace Mewdeko.Modules.OwnerOnly;

/// <summary>
/// Manages instances for use with the dashboard
/// </summary>
[OwnerOnly]
public class InstanceManagement(InteractiveService interactiveService, DbContextProvider provider) : MewdekoModuleBase<BotInstanceService>
{
    /// <summary>
    /// Adds an instance to be managed from the dashboard
    /// </summary>
    /// <param name="instanceUrl"></param>
    [Cmd, Aliases]
    public async Task AddInstance([Remainder] string instanceUrl)
    {
        try
        {
            var uri = new Uri(instanceUrl);
            var added = await Service.AddInstanceAsync(uri);
            if (added.Item1)
            {
                var eb = new EmbedBuilder()
                    .WithTitle($"{Strings.InstanceAdded(ctx.Guild.Id)} {added.Item2.BotName}")
                    .WithThumbnailUrl(added.Item2.BotAvatar)
                    .WithDescription($"{Strings.InstanceStatus(ctx.Guild.Id)} {added.Item2.BotStatus}" +
                                     $"\n {Strings.InstanceVersion(ctx.Guild.Id, added.Item2.BotVersion)}" +
                                     $"\n {Strings.InstanceCommandCount(ctx.Guild.Id, added.Item2.CommandsCount)}" +
                                     $"\n {Strings.InstanceModulesCount(ctx.Guild.Id, added.Item2.ModulesCount)}" +
                                     $"\n {Strings.InstanceUserCount(ctx.Guild.Id, added.Item2.UserCount)}")
                    .WithOkColor();
                await ctx.Channel.SendMessageAsync(embed: eb.Build());

            }
            else
                await ErrorAsync(Strings.InstanceNotAdded(ctx.Guild.Id, added.Item3));
        }
        catch (UriFormatException)
        {
            await ErrorAsync(Strings.InvalidInstanceUrl(ctx.Guild.Id));
        }
    }

    /// <summary>
    /// Lists all added instances for use with dashboard management
    /// </summary>
    [Cmd, Alias]
    public async Task ListInstances()
    {
        await using var db = await provider.GetContextAsync();
        var instances = await db.BotInstances.ToListAsyncLinqToDB();
    }
}
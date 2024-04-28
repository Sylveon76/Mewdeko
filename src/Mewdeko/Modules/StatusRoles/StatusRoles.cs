﻿using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.StatusRoles.Services;
using Mewdeko.Services.Settings;

namespace Mewdeko.Modules.StatusRoles;

/// <summary>
/// Module for managing roles that are assigned based on user status.
/// </summary>
public class StatusRoles(BotConfigService bss, InteractiveService interactivity) : MewdekoModuleBase<StatusRolesService>
{
    /// <summary>
    /// Adds a status role configuration.
    /// </summary>
    /// <param name="status">The status to add.</param>
    [Cmd, Aliases, UserPerm(GuildPermission.ManageGuild)]
    public async Task AddStatusRole([Remainder] string status)
    {
        if (status.Length > 128)
        {
            await ctx.Channel.SendErrorAsync(
                $"{bss.Data.ErrorEmote} That's too long to even fit in a normal status. Try again.", Config);
            return;
        }

        var added = await Service.AddStatusRoleConfig(status, ctx.Guild.Id);
        if (added)
            await ctx.Channel.SendConfirmAsync("Added StatusRole config! Please configure it with the other commands.");
        else
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} That StatusRole already exists!", Config);
    }

    /// <summary>
    /// Removes a status role configuration.
    /// </summary>
    /// <param name="index">The index of the status role to remove.</param>
    [Cmd, Aliases, UserPerm(GuildPermission.ManageGuild)]
    public async Task RemoveStatusRole(int index)
    {
        var statusRoles = await Service.GetStatusRoleConfig(ctx.Guild.Id);
        if (!statusRoles.Any())
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} There are no configured StatusRoles!", Config);
            return;
        }

        var potentialStatusRole = statusRoles.ElementAt(index - 1);
        if (potentialStatusRole is null)
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} No StatusRole found with that ID!", Config);
            return;
        }

        await Service.RemoveStatusRoleConfig(potentialStatusRole);
        await ctx.Channel.SendConfirmAsync("StatusRole config removed!");
    }

    /// <summary>
    /// Sets or previews the embed text for a specific status role.
    /// </summary>
    /// <param name="index">The index of the status role.</param>
    /// <param name="embedText">The embed text to set.</param>
    [Cmd, Aliases, UserPerm(GuildPermission.ManageGuild)]
    public async Task SetStatusRoleEmbed(int index, [Remainder] string embedText = null)
    {
        var statusRoles = await Service.GetStatusRoleConfig(ctx.Guild.Id);
        if (!statusRoles.Any())
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} There are no configured StatusRoles!", Config);
            return;
        }

        var potentialStatusRole = statusRoles.ElementAt(index - 1);
        if (potentialStatusRole is null)
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} No StatusRole found with that ID!", Config);
            return;
        }

        if (string.IsNullOrWhiteSpace(embedText))
        {
            if (string.IsNullOrWhiteSpace(potentialStatusRole.StatusEmbed))
            {
                await ctx.Channel.SendErrorAsync(
                    $"{bss.Data.ErrorEmote} There is no embed/text set for this StatusRole! Please include embed json or text to preview it!",
                    Config);
                return;
            }

            var componentBuilder = new ComponentBuilder()
                .WithButton("Preview", "preview")
                .WithButton("View Raw", "viewraw");

            var msgid = await ctx.Channel.SendMessageAsync(embed: new EmbedBuilder()
                .WithOkColor()
                .WithDescription(
                    $"{bss.Data.LoadingEmote} Please select what you want to do with the current StatusRole text")
                .Build(), components: componentBuilder.Build());

            var button = await GetButtonInputAsync(ctx.Channel.Id, msgid.Id, ctx.User.Id);
            switch (button)
            {
                case "preview":
                    var rep = new ReplacementBuilder()
                        .WithDefault(ctx).Build();
                    if (SmartEmbed.TryParse(rep.Replace(potentialStatusRole.StatusEmbed), ctx.Guild.Id, out var embeds,
                            out var plainText, out var components))
                        await ctx.Channel.SendMessageAsync(plainText, embeds: embeds, components: components.Build());
                    else
                        await ctx.Channel.SendMessageAsync(rep.Replace(potentialStatusRole.StatusEmbed));
                    break;
                case "viewraw":
                    await ctx.Channel.SendConfirmAsync(potentialStatusRole.StatusEmbed);
                    break;
                default:
                    await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} Timed out.", Config);
                    break;
            }
        }
        else
        {
            await Service.SetStatusEmbed(potentialStatusRole, embedText);
            await ctx.Channel.SendConfirmAsync($"{bss.Data.SuccessEmote} Succesfully set embed text!");
        }
    }

    /// <summary>
    /// Sets the channel for a specific status role.
    /// </summary>
    /// <param name="index">The index of the status role.</param>
    /// <param name="channel">The channel to set.</param>
    [Cmd, Aliases, UserPerm(GuildPermission.ManageGuild)]
    public async Task SetStatusRoleChannel(int index, ITextChannel channel)
    {
        var statusRoles = await Service.GetStatusRoleConfig(ctx.Guild.Id);
        if (!statusRoles.Any())
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} There are no configured StatusRoles!", Config);
            return;
        }

        var potentialStatusRole = statusRoles.ElementAt(index - 1);
        if (potentialStatusRole is null)
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} No StatusRole found with that ID!", Config);
            return;
        }

        if (potentialStatusRole.StatusChannelId == channel.Id)
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} That's already your StatusEmbedChannel.", Config);
            return;
        }

        await Service.SetStatusChannel(potentialStatusRole, channel.Id);
        await ctx.Channel.SendConfirmAsync(
            $"{bss.Data.SuccessEmote} Succesfully set StatusEmbedChannel to {channel.Mention}!");
    }

    /// <summary>
    /// Sets the roles to add when a user has the selected status.
    /// </summary>
    /// <param name="index">The index of the status role.</param>
    /// <param name="roles">The roles to add.</param>
    [Cmd, Aliases, UserPerm(GuildPermission.ManageGuild)]
    public async Task SetAddRoles(int index, params IRole[] roles)
    {
        var statusRoles = await Service.GetStatusRoleConfig(ctx.Guild.Id);
        if (!statusRoles.Any())
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} There are no configured StatusRoles!", Config);
            return;
        }

        var potentialStatusRole = statusRoles.ElementAt(index - 1);
        if (potentialStatusRole is null)
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} No StatusRole found with that ID!", Config);
            return;
        }

        if (string.IsNullOrWhiteSpace(potentialStatusRole.ToAdd))
        {
            var splitRoleIds = string.Join(" ", roles.Select(x => x.Id));
            await Service.SetAddRoles(potentialStatusRole, splitRoleIds);
            await ctx.Channel.SendConfirmAsync(
                $"{bss.Data.SuccessEmote} Having this status will now add the following roles:\n{string.Join("|", roles.Select(x => x.Mention))}");
        }
        else
        {
            var toModify = potentialStatusRole.ToAdd.Split(" ").ToList();
            toModify.AddRange(roles.Select(x => x.Id.ToString()));
            await Service.SetAddRoles(potentialStatusRole, string.Join(" ", toModify));
            await ctx.Channel.SendConfirmAsync(
                $"{bss.Data.SuccessEmote} Having this status will now add the following roles:\n{string.Join("|", toModify.Select(x => $"<@&{x}>"))}");
        }
    }

    /// <summary>
    /// Sets the roles to remove when a user has the selected status.
    /// </summary>
    /// <param name="index">The index of the status role.</param>
    /// <param name="roles">The roles to remove.</param>
    [Cmd, Aliases, UserPerm(GuildPermission.ManageGuild)]
    public async Task SetRemoveRoles(int index, params IRole[] roles)
    {
        var statusRoles = await Service.GetStatusRoleConfig(ctx.Guild.Id);
        if (!statusRoles.Any())
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} There are no configured StatusRoles!", Config);
            return;
        }

        var potentialStatusRole = statusRoles.ElementAt(index - 1);
        if (potentialStatusRole is null)
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} No StatusRole found with that ID!", Config);
            return;
        }

        if (string.IsNullOrWhiteSpace(potentialStatusRole.ToRemove))
        {
            var splitRoleIds = string.Join(" ", roles.Select(x => x.Id));
            await Service.SetRemoveRoles(potentialStatusRole, splitRoleIds);
            await ctx.Channel.SendConfirmAsync(
                $"{bss.Data.SuccessEmote} Having this status will now remove the following roles:\n{string.Join("|", roles.Select(x => x.Mention))}");
        }
        else
        {
            var toModify = potentialStatusRole.ToRemove.Split(" ").ToList();
            toModify.AddRange(roles.Select(x => x.Id.ToString()));
            await Service.SetRemoveRoles(potentialStatusRole, string.Join(" ", toModify));
            await ctx.Channel.SendConfirmAsync(
                $"{bss.Data.SuccessEmote} Having this status will now remove the following roles:\n{string.Join("|", toModify.Select(x => $"<@&{x}>"))}");
        }
    }

    /// <summary>
    /// Sets the roles to remove when a user has the selected status.
    /// </summary>
    /// <param name="index">The index of the status role.</param>
    /// <param name="roles">The roles to remove.</param>
    [Cmd, Aliases, UserPerm(GuildPermission.ManageGuild)]
    public async Task RemoveAddRoles(int index, params IRole[] roles)
    {
        var statusRoles = await Service.GetStatusRoleConfig(ctx.Guild.Id);
        if (!statusRoles.Any())
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} There are no configured StatusRoles!", Config);
            return;
        }

        var potentialStatusRole = statusRoles.ElementAt(index - 1);
        if (potentialStatusRole is null)
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} No StatusRole found with that ID!", Config);
            return;
        }

        var addRoles = potentialStatusRole.ToAdd.Split(" ");
        var newList = addRoles.Except(roles.Select(x => $"{x.Id}")).ToList();
        if (addRoles.Length == newList.Count)
        {
            await ctx.Channel.SendErrorAsync(
                $"{bss.Data.ErrorEmote} No AddRoles removed, none of the provided roles are in the list.", Config);
            return;
        }

        await Service.SetAddRoles(potentialStatusRole, string.Join(" ", newList));
        await ctx.Channel.SendConfirmAsync(
            $"{bss.Data.SuccessEmote} Succesfully removed the following roles from AddRoles\n{string.Join("|", roles.Select(x => x.Mention))}");
    }

    /// <summary>
    /// Removes one or more roles from the roles added when a user has the selected status.
    /// </summary>
    /// <param name="index">The index of the status role.</param>
    /// <param name="roles">The roles to remove.</param>
    [Cmd, Aliases, UserPerm(GuildPermission.ManageGuild)]
    public async Task RemoveRemoveRoles(int index, params IRole[] roles)
    {
        var statusRoles = await Service.GetStatusRoleConfig(ctx.Guild.Id);
        if (!statusRoles.Any())
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} There are no configured StatusRoles!", Config);
            return;
        }

        var potentialStatusRole = statusRoles.ElementAt(index - 1);
        if (potentialStatusRole is null)
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} No StatusRole found with that ID!", Config);
            return;
        }

        var removeRoles = potentialStatusRole.ToRemove.Split(" ");
        var newList = removeRoles.Except(roles.Select(x => $"{x.Id}")).ToList();
        if (removeRoles.Length == newList.Count)
        {
            await ctx.Channel.SendErrorAsync(
                $"{bss.Data.ErrorEmote} No RemoveRoles removed, none of the provided roles are in the list.", Config);
            return;
        }

        await Service.SetRemoveRoles(potentialStatusRole, string.Join(" ", newList));
        await ctx.Channel.SendConfirmAsync(
            $"{bss.Data.SuccessEmote} Succesfully removed the following roles from RemoveRoles\n{string.Join("|", roles.Select(x => x.Mention))}");
    }

    /// <summary>
    /// Toggles whether added roles are removed when a user no longer has a status by the provided index
    /// </summary>
    /// <param name="index">The index of the status role.</param>
    [Cmd, Aliases, UserPerm(GuildPermission.ManageGuild)]
    public async Task ToggleRemoveAdded(int index)
    {
        var statusRoles = await Service.GetStatusRoleConfig(ctx.Guild.Id);
        if (!statusRoles.Any())
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} There are no configured StatusRoles!", Config);
            return;
        }

        var potentialStatusRole = statusRoles.ElementAt(index - 1);
        if (potentialStatusRole is null)
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} No StatusRole found with that ID!", Config);
            return;
        }

        var returned = await Service.ToggleRemoveAdded(potentialStatusRole);
        await ctx.Channel.SendConfirmAsync($"{bss.Data.SuccessEmote} RemoveAdded is now `{returned}`");
    }

    /// <summary>
    /// Toggles whether added roles are removed when a status is removed.
    /// </summary>
    /// <param name="index">The index of the status role.</param>
    [Cmd, Aliases, UserPerm(GuildPermission.ManageGuild)]
    public async Task ToggleReaddRemoved(int index)
    {
        var statusRoles = await Service.GetStatusRoleConfig(ctx.Guild.Id);
        if (!statusRoles.Any())
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} There are no configured StatusRoles!", Config);
            return;
        }

        var potentialStatusRole = statusRoles.ElementAt(index - 1);
        if (potentialStatusRole is null)
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} No StatusRole found with that ID!", Config);
            return;
        }

        var returned = await Service.ToggleAddRemoved(potentialStatusRole);
        await ctx.Channel.SendConfirmAsync($"{bss.Data.SuccessEmote} ReaddRemoved is now `{returned}`");
    }

    /// <summary>
    /// Lists all current status roles with their details.
    /// </summary>
    [Cmd, Aliases, UserPerm(GuildPermission.ManageGuild)]
    public async Task ListStatusRoles()
    {
        var statusRoles = await Service.GetStatusRoleConfig(ctx.Guild.Id);
        if (!statusRoles.Any())
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} There are no configured StatusRoles!", Config);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(statusRoles.Count() - 1)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, Context.Channel,
            TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            var statusArray = statusRoles.ToArray();
            var curStatusRole = statusArray.Skip(page).FirstOrDefault();
            return new PageBuilder().WithDescription(
                    $"#{Array.IndexOf(statusArray, curStatusRole) + 1}" +
                    $"\n`Status`: {curStatusRole.Status.TrimTo(30)}" +
                    $"\n`Channel:` {((await ctx.Guild.GetTextChannelAsync(curStatusRole.StatusChannelId).ConfigureAwait(false))?.Mention == null ? "Deleted" : (await ctx.Guild.GetTextChannelAsync(curStatusRole.StatusChannelId).ConfigureAwait(false))?.Mention)} {curStatusRole.StatusChannelId}" +
                    $"\n`AddRoles`: {(!string.IsNullOrEmpty(curStatusRole.ToAdd) ? string.Join("|", curStatusRole.ToAdd.Split(" ").Select(x => $"<@&{x}>")) : "None")}" +
                    $"\n`RemoveRoles`: {(!string.IsNullOrEmpty(curStatusRole.ToRemove) ? string.Join("|", curStatusRole.ToRemove.Split(" ").Select(x => $"<@&{x}>")) : "None")}" +
                    $"\n`RemoveAdded`: {curStatusRole.RemoveAdded}" +
                    $"\n`ReaddRemoved`: {curStatusRole.ReaddRemoved}" +
                    $"\n`Message:` {(curStatusRole.StatusEmbed.IsNullOrWhiteSpace() ? "None" : curStatusRole.StatusEmbed.TrimTo(100))}")
                .WithOkColor();
        }
    }
}
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Tickets.Services;
using System.Text.Json;
using Serilog;

namespace Mewdeko.Modules.Tickets;

/// <summary>
///     Commands for managing the ticket system, including panels, tickets, cases,
///     and administrative functions.
/// </summary>
public partial class Tickets : MewdekoModuleBase<TicketService>
{
    /// <summary>
    /// Creates a ticket panel
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task TicketPanel(ITextChannel channel, [Remainder] string embedJson = null)
    {
        try
        {
            if (embedJson is null)
            {
                // Show preview/regular option
                var components = new ComponentBuilder()
                    .WithButton("Preview", "preview")
                    .WithButton("Create", "create");

                var msg = await ctx.Channel.SendMessageAsync(
                    embed: new EmbedBuilder().WithDescription("Would you like to preview the panel or create it with default settings?").WithOkColor().Build(),
                    components: components.Build()
                );

                var response = await GetButtonInputAsync(ctx.Channel.Id, msg.Id, ctx.User.Id);
                await msg.DeleteAsync();

                switch (response)
                {
                    case "preview":
                        // Show default panel preview
                        var defaultEmbed = new
                        {
                            title = "Support Tickets",
                            description = "Click a button below to create a ticket",
                            color = "blue"
                        };
                        await Service.PreviewPanelAsync(channel, JsonSerializer.Serialize(new[] { defaultEmbed }));
                        return;

                    case "create":
                        var panel = await Service.CreatePanelAsync(channel);
                        await ctx.Channel.SendConfirmAsync($"Panel created in {channel.Mention}!");
                        return;
                }
            }
            else
            {
                // Show preview option for custom JSON
                var components = new ComponentBuilder()
                    .WithButton("Preview", "preview")
                    .WithButton("Create", "create");

                var msg = await ctx.Channel.SendMessageAsync(
                    embed: new EmbedBuilder().WithDescription("Would you like to preview the panel or create it?").WithOkColor().Build(),
                    components: components.Build()
                );

                var response = await GetButtonInputAsync(ctx.Channel.Id, msg.Id, ctx.User.Id);
                await msg.DeleteAsync();

                switch (response)
                {
                    case "preview":
                        await Service.PreviewPanelAsync(channel, embedJson);
                        return;

                    case "create":
                        var panel = await Service.CreatePanelAsync(channel, embedJson);
                        await ctx.Channel.SendConfirmAsync($"Panel created in {channel.Mention}!");
                        return;
                }
            }
        }
        catch (ArgumentException ex)
        {
            await ctx.Channel.SendErrorAsync(ex.Message, Config);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating ticket panel");
            await ctx.Channel.SendErrorAsync("Failed to create ticket panel.", Config);
        }
    }

    /// <summary>
    ///     Adds a button to an existing ticket panel.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task TicketAddButton(ulong panelId, string label, ButtonStyle style = ButtonStyle.Primary, string? emoji = null)
    {
        var panel = await Service.GetPanelAsync(panelId);
        if (panel == null || panel.GuildId != ctx.Guild.Id)
        {
            await ctx.Channel.SendErrorAsync(Strings.TicketPanelNotFound(ctx.Guild.Id), Config);
            return;
        }

        await Service.AddButtonAsync(panel, label, emoji, style);
        await ctx.Channel.SendConfirmAsync(Strings.TicketButtonAdded(ctx.Guild.Id, label, panelId));
    }

    /// <summary>
    ///     Deletes an existing ticket panel.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task TicketPanelDelete(ulong panelId)
    {
        await Service.DeletePanelAsync(panelId, ctx.Guild);
        await ctx.Channel.SendConfirmAsync(Strings.TicketPanelDeleted(ctx.Guild.Id, panelId));
    }

    /// <summary>
    ///     Sets the category where tickets will be created.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task TicketCategory(int buttonId, ICategoryChannel? category = null)
    {
        var panel = await Service.GetButtonAsync(buttonId);
        if (panel == null || panel.Panel.GuildId != ctx.Guild.Id)
        {
            await ctx.Channel.SendErrorAsync(Strings.TicketPanelNotFound(ctx.Guild.Id), Config);
            return;
        }

        await Service.UpdateButtonSettingsAsync(ctx.Guild, buttonId, new Dictionary<string, object>
        {
            { "categoryId", category?.Id }
        });

        if (category == null)
            await ctx.Channel.SendConfirmAsync(Strings.TicketCategoryRemoved(ctx.Guild.Id, buttonId));
        else
            await ctx.Channel.SendConfirmAsync(Strings.TicketCategorySet(ctx.Guild.Id, buttonId, category.Name));
    }

    /// <summary>
    ///     Sets the category where closed tickets will be archived.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task TicketArchiveCategory(int buttonId, ICategoryChannel? category = null)
    {
        var panel = await Service.GetButtonAsync(buttonId);
        if (panel == null || panel.Panel.GuildId != ctx.Guild.Id)
        {
            await ctx.Channel.SendErrorAsync(Strings.TicketPanelNotFound(ctx.Guild.Id), Config);
            return;
        }

        await Service.UpdateButtonSettingsAsync(ctx.Guild, buttonId, new Dictionary<string, object>
        {
            { "archiveCategoryId", category?.Id }
        });

        if (category == null)
            await ctx.Channel.SendConfirmAsync(Strings.TicketArchiveCategoryRemoved(ctx.Guild.Id, buttonId));
        else
            await ctx.Channel.SendConfirmAsync(Strings.TicketArchiveCategorySet(ctx.Guild.Id, buttonId, category.Name));
    }

    /// <summary>
    ///     Claims the current ticket as a staff member.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TicketClaim()
    {
        var success = await Service.ClaimTicket(ctx.Guild, ctx.Channel.Id, ctx.User as IGuildUser);
        if (success)
            await ctx.Channel.SendConfirmAsync(Strings.TicketClaimed(ctx.Guild.Id, ctx.User.Mention));
        else
            await ctx.Channel.SendErrorAsync(Strings.TicketClaimFailed(ctx.Guild.Id), Config);
    }

    /// <summary>
    ///     Unclaims the current ticket.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TicketUnclaim()
    {
        var success = await Service.UnclaimTicket(ctx.Guild, ctx.Channel.Id, ctx.User as IGuildUser);
        if (success)
            await ctx.Channel.SendConfirmAsync(Strings.TicketUnclaimed(ctx.Guild.Id));
        else
            await ctx.Channel.SendErrorAsync(Strings.TicketUnclaimFailed(ctx.Guild.Id), Config);
    }

    /// <summary>
    ///     Closes the current ticket.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TicketClose()
    {
        var success = await Service.CloseTicket(ctx.Guild, ctx.Channel.Id);
        if (success)
            await ctx.Channel.SendConfirmAsync(Strings.TicketClosed(ctx.Guild.Id));
        else
            await ctx.Channel.SendErrorAsync(Strings.TicketCloseFailed(ctx.Guild.Id), Config);
    }

    /// <summary>
    ///     Archives a ticket, moving it to the archive category if set.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task TicketArchive()
    {
        var ticket = await Service.GetTicketAsync(ctx.Channel.Id);
        if (ticket == null)
        {
            await ctx.Channel.SendErrorAsync(Strings.TicketNotFound(ctx.Guild.Id), Config);
            return;
        }

        await Service.ArchiveTicketAsync(ticket);
        await ctx.Channel.SendConfirmAsync(Strings.TicketArchived(ctx.Guild.Id));
    }

    /// <summary>
    ///     Creates a new ticket case.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task TicketCreateCase(string title, [Remainder] string description)
    {
        var ticketCase = await Service.CreateCaseAsync(ctx.Guild, title, description, ctx.User as IGuildUser);
        await ctx.Channel.SendConfirmAsync(Strings.TicketCaseCreated(ctx.Guild.Id, ticketCase.Id));
    }

    /// <summary>
    ///     Links the current ticket to a case.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task TicketLinkCase(int caseId)
    {
        var ticket = await Service.GetTicketAsync(ctx.Channel.Id);
        var success = await Service.AddTicketToCase(ctx.Guild.Id, caseId, ticket.Id);
        if (success)
            await ctx.Channel.SendConfirmAsync(Strings.TicketLinkedToCase(ctx.Guild.Id, caseId));
        else
            await ctx.Channel.SendErrorAsync(Strings.TicketLinkFailed(ctx.Guild.Id), Config);
    }

    /// <summary>
    ///     Unlinks the current ticket from its case.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task TicketUnlinkCase()
    {
        var ticket = await Service.GetTicketAsync(ctx.Channel.Id);
        var success = await Service.RemoveTicketFromCase(ctx.Guild.Id, ticket.Id);
        if (success)
            await ctx.Channel.SendConfirmAsync(Strings.TicketUnlinkedFromCase(ctx.Guild.Id));
        else
            await ctx.Channel.SendErrorAsync(Strings.TicketUnlinkFailed(ctx.Guild.Id), Config);
    }

    /// <summary>
    ///     Sets the priority level for the current ticket.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TicketPriority(string priority)
    {
        var success = await Service.SetTicketPriority(ctx.Guild, ctx.Channel.Id, priority, ctx.User as IGuildUser);
        if (success)
            await ctx.Channel.SendConfirmAsync(Strings.TicketPrioritySet(ctx.Guild.Id, priority));
        else
            await ctx.Channel.SendErrorAsync(Strings.TicketPriorityFailed(ctx.Guild.Id), Config);
    }

    /// <summary>
    ///     Creates a new priority level for tickets.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task TicketAddPriority(string id, string name, string emoji, int level, bool pingStaff, TimeSpan responseTime, [Remainder] string color)
    {
        if (!ColorUtils.TryParseColor(color, out var parsedColor))
        {
            await ctx.Channel.SendErrorAsync(Strings.InvalidColorFormat(ctx.Guild.Id), Config);
            return;
        }

        var success = await Service.CreatePriority(ctx.Guild.Id, id, name, emoji, level, pingStaff, responseTime, parsedColor);
        if (success)
            await ctx.Channel.SendConfirmAsync(Strings.TicketPriorityCreated(ctx.Guild.Id, name));
        else
            await ctx.Channel.SendErrorAsync(Strings.TicketPriorityCreateFailed(ctx.Guild.Id), Config);
    }

    /// <summary>
    ///     Deletes a priority level.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task TicketRemovePriority(string priorityId)
    {
        var success = await Service.DeletePriority(ctx.Guild.Id, priorityId);
        if (success)
            await ctx.Channel.SendConfirmAsync(Strings.TicketPriorityDeleted(ctx.Guild.Id, priorityId));
        else
            await ctx.Channel.SendErrorAsync(Strings.TicketPriorityDeleteFailed(ctx.Guild.Id), Config);
    }

    /// <summary>
    ///     Creates a new tag for tickets.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task TicketAddTag(string id, string name, string description, [Remainder] string color)
    {
        if (!ColorUtils.TryParseColor(color, out var parsedColor))
        {
            await ctx.Channel.SendErrorAsync(Strings.InvalidColorFormat(ctx.Guild.Id), Config);
            return;
        }

        var success = await Service.CreateTag(ctx.Guild.Id, id, name, description, parsedColor);
        if (success)
            await ctx.Channel.SendConfirmAsync(Strings.TicketTagCreated(ctx.Guild.Id, name));
        else
            await ctx.Channel.SendErrorAsync(Strings.TicketTagCreateFailed(ctx.Guild.Id), Config);
    }

    /// <summary>
    ///     Removes a tag.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task TicketRemoveTag(string tagId)
    {
        var success = await Service.DeleteTag(ctx.Guild.Id, tagId);
        if (success)
            await ctx.Channel.SendConfirmAsync(Strings.TicketTagDeleted(ctx.Guild.Id, tagId));
        else
            await ctx.Channel.SendErrorAsync(Strings.TicketTagDeleteFailed(ctx.Guild.Id), Config);
    }

    /// <summary>
    ///     Adds tags to the current ticket.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TicketAddTags(params string[] tags)
    {
        var success = await Service.AddTicketTags(ctx.Guild, ctx.Channel.Id, tags, ctx.User as IGuildUser);
        if (success)
            await ctx.Channel.SendConfirmAsync(Strings.TicketTagsAdded(ctx.Guild.Id));
        else
            await ctx.Channel.SendErrorAsync(Strings.TicketTagsAddFailed(ctx.Guild.Id), Config);
    }

    /// <summary>
    ///     Removes tags from the current ticket.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TicketRemoveTags(params string[] tags)
    {
        var success = await Service.RemoveTicketTags(ctx.Guild, ctx.Channel.Id, tags, ctx.User as IGuildUser);
        if (success)
            await ctx.Channel.SendConfirmAsync(Strings.TicketTagsRemoved(ctx.Guild.Id));
        else
            await ctx.Channel.SendErrorAsync(Strings.TicketTagsRemoveFailed(ctx.Guild.Id), Config);
    }

    /// <summary>
    ///     Blocks a user from creating tickets.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task TicketBlock(IGuildUser user, [Remainder] string? reason = null)
    {
        var success = await Service.BlacklistUser(ctx.Guild, user.Id, reason);
        if (success)
            await ctx.Channel.SendConfirmAsync(Strings.TicketUserBlocked(ctx.Guild.Id, user.Mention));
        else
            await ctx.Channel.SendErrorAsync(Strings.TicketUserAlreadyBlocked(ctx.Guild.Id), Config);
    }

    /// <summary>
    ///     Unblocks a user from creating tickets.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task TicketUnblock(IGuildUser user)
    {
        var success = await Service.UnblacklistUser(ctx.Guild, user.Id);
        if (success)
            await ctx.Channel.SendConfirmAsync(Strings.TicketUserUnblocked(ctx.Guild.Id, user.Mention));
        else
            await ctx.Channel.SendErrorAsync(Strings.TicketUserNotBlocked(ctx.Guild.Id), Config);
    }

    /// <summary>
    ///     Shows ticket statistics for the guild.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TicketStats()
    {
        var stats = await Service.GetGuildStatistics(ctx.Guild.Id);

        var eb = new EmbedBuilder()
            .WithTitle(Strings.TicketStatsTitle(ctx.Guild.Id))
            .AddField(Strings.TotalTickets(ctx.Guild.Id), stats.TotalTickets, true)
            .AddField(Strings.OpenTickets(ctx.Guild.Id), stats.OpenTickets, true)
            .AddField(Strings.ClosedTickets(ctx.Guild.Id), stats.ClosedTickets, true)
            .AddField(Strings.AverageResponseTime(ctx.Guild.Id),
                $"{stats.AverageResponseTime:F1} " + Strings.Minutes(ctx.Guild.Id), true)
            .AddField(Strings.AverageResolutionTime(ctx.Guild.Id),
                $"{stats.AverageResolutionTime:F1} " + Strings.Hours(ctx.Guild.Id), true);

        if (stats.TicketsByType.Any())
        {
            var typeStats = string.Join("\n", stats.TicketsByType.Select(t =>
                $"{t.Key}: {t.Value}"));
            eb.AddField(Strings.TicketsByType(ctx.Guild.Id), typeStats);
        }

        if (stats.TicketsByPriority.Any())
        {
            var priorityStats = string.Join("\n", stats.TicketsByPriority.Select(p =>
                $"{p.Key}: {p.Value}"));
            eb.AddField(Strings.TicketsByPriority(ctx.Guild.Id), priorityStats);
        }

        await ctx.Channel.SendMessageAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Shows ticket statistics for a user.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TicketUserStats(IGuildUser? user = null)
    {
        user ??= ctx.User as IGuildUser;
        var stats = await Service.GetUserStatistics(ctx.Guild.Id, user.Id);

        var eb = new EmbedBuilder()
            .WithTitle(Strings.UserTicketStats(ctx.Guild.Id, user.Username))
            .AddField(Strings.TotalTickets(ctx.Guild.Id), stats.TotalTickets, true)
            .AddField(Strings.OpenTickets(ctx.Guild.Id), stats.OpenTickets, true)
            .AddField(Strings.ClosedTickets(ctx.Guild.Id), stats.ClosedTickets, true);

        if (stats.TicketsByType.Any())
        {
            var typeStats = string.Join("\n", stats.TicketsByType.Select(t =>
                $"{t.Key}: {t.Value}"));
            eb.AddField(Strings.TicketsByType(ctx.Guild.Id), typeStats);
        }

        if (stats.RecentTickets.Any())
        {
            var recentTickets = string.Join("\n", stats.RecentTickets.Select(t =>
                $"#{t.TicketId} - {t.Type} - {(t.ClosedAt.HasValue ? Strings.Closed(ctx.Guild.Id) : Strings.Open(ctx.Guild.Id))}"));
            eb.AddField(Strings.RecentTickets(ctx.Guild.Id), recentTickets);
        }

        await ctx.Channel.SendMessageAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Sets the transcript channel for tickets.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task TicketTranscriptChannel(ITextChannel? channel = null)
    {
        await Service.SetTranscriptChannelAsync(ctx.Guild.Id, channel?.Id ?? 0);

        if (channel == null)
            await ctx.Channel.SendConfirmAsync(Strings.TranscriptChannelRemoved(ctx.Guild.Id));
        else
            await ctx.Channel.SendConfirmAsync(Strings.TranscriptChannelSet(ctx.Guild.Id, channel.Mention));
    }

    /// <summary>
    ///     Sets the log channel for tickets.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task TicketLogChannel(ITextChannel? channel = null)
    {
        await Service.SetLogChannelAsync(ctx.Guild.Id, channel?.Id ?? 0);

        if (channel == null)
            await ctx.Channel.SendConfirmAsync(Strings.LogChannelRemoved(ctx.Guild.Id));
        else
            await ctx.Channel.SendConfirmAsync(Strings.LogChannelSet(ctx.Guild.Id, channel.Mention));
    }

    /// <summary>
    ///     Moves all tickets between categories.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task TicketMoveAll(ICategoryChannel sourceCategory, ICategoryChannel targetCategory)
    {
        var (moved, failed) = await Service.BatchMoveTickets(ctx.Guild, sourceCategory.Id, targetCategory.Id);
        await ctx.Channel.SendConfirmAsync(
            Strings.TicketsMoved(ctx.Guild.Id, moved, failed, sourceCategory.Name, targetCategory.Name));
    }

    /// <summary>
    ///     Closes inactive tickets.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task TicketCloseInactive(int hours)
    {
        var (closed, failed) = await Service.BatchCloseInactiveTickets(ctx.Guild, TimeSpan.FromHours(hours));
        await ctx.Channel.SendConfirmAsync(Strings.InactiveTicketsClosed(ctx.Guild.Id, closed, failed, hours));
    }

    /// <summary>
    ///     Sets the required response time for a ticket type.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task TicketResponseTime(int buttonId, int? minutes = null)
    {
        var success = await Service.UpdateRequiredResponseTimeAsync(ctx.Guild, buttonId,
            minutes.HasValue ? TimeSpan.FromMinutes(minutes.Value) : null);

        if (success)
        {
            if (minutes.HasValue)
                await ctx.Channel.SendConfirmAsync(Strings.ResponseTimeSet(ctx.Guild.Id, minutes.Value));
            else
                await ctx.Channel.SendConfirmAsync(Strings.ResponseTimeRemoved(ctx.Guild.Id));
        }
        else
        {
            await ctx.Channel.SendErrorAsync(Strings.ResponseTimeUpdateFailed(ctx.Guild.Id), Config);
        }
    }

    /// <summary>
    ///     Duplicates a ticket panel.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task TicketDuplicatePanel(ulong panelId, ITextChannel targetChannel)
    {
        var newPanel = await Service.DuplicatePanelAsync(ctx.Guild, panelId, targetChannel.Id);
        if (newPanel != null)
            await ctx.Channel.SendConfirmAsync(Strings.PanelDuplicated(ctx.Guild.Id, panelId, targetChannel.Mention));
        else
            await ctx.Channel.SendErrorAsync(Strings.PanelDuplicateFailed(ctx.Guild.Id), Config);
    }

    /// <summary>
    ///     Updates a ticket panel's embed.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task TicketUpdatePanel(ulong panelId, [Remainder] string embedJson)
    {
        var success = await Service.UpdatePanelEmbedAsync(ctx.Guild, panelId, embedJson);
        if (success)
            await ctx.Channel.SendConfirmAsync(Strings.PanelUpdated(ctx.Guild.Id));
        else
            await ctx.Channel.SendErrorAsync(Strings.PanelUpdateFailed(ctx.Guild.Id), Config);
    }

    /// <summary>
    ///     Moves a ticket panel to another channel.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task TicketMovePanel(ulong panelId, ITextChannel targetChannel)
    {
        var success = await Service.MovePanelAsync(ctx.Guild, panelId, targetChannel.Id);
        if (success)
            await ctx.Channel.SendConfirmAsync(Strings.PanelMoved(ctx.Guild.Id, panelId, targetChannel.Mention));
        else
            await ctx.Channel.SendErrorAsync(Strings.PanelMoveFailed(ctx.Guild.Id), Config);
    }

    /// <summary>
    ///     Sets auto-close time for a ticket type.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task TicketAutoClose(int buttonId, int? hours = null)
    {
        await Service.UpdateButtonSettingsAsync(ctx.Guild, buttonId, new Dictionary<string, object>
        {
            { "autoCloseTime", hours.HasValue ? TimeSpan.FromHours(hours.Value) : null }
        });

        if (hours.HasValue)
            await ctx.Channel.SendConfirmAsync(Strings.AutoCloseSet(ctx.Guild.Id, hours.Value));
        else
            await ctx.Channel.SendConfirmAsync(Strings.AutoCloseDisabled(ctx.Guild.Id));
    }

    /// <summary>
    ///     Adds a note to the current ticket.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TicketNote([Remainder] string content)
    {
        var success = await Service.AddNote(ctx.Channel.Id, ctx.User as IGuildUser, content);
        if (success)
            await ctx.Channel.SendConfirmAsync(Strings.NoteAdded(ctx.Guild.Id));
        else
            await ctx.Channel.SendErrorAsync(Strings.NoteAddFailed(ctx.Guild.Id), Config);
    }
}
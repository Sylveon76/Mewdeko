using System.Text;
using System.Text.Json;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Tickets.Services;
using Serilog;
using StackExchange.Redis;

namespace Mewdeko.Modules.Tickets;

/// <summary>
///     Provides commands for managing the ticket system.
/// </summary>
[Group("tickets", "Manage the ticket system.")]
public class TicketCommands : MewdekoSlashModuleBase<TicketService>
{
    private readonly InteractiveService _interactivity;
    private readonly IDataCache cache;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TicketCommands" /> class.
    /// </summary>
    /// <param name="interactivity">The interactive service.</param>
    /// <param name="cache">The cache service.</param>
    public TicketCommands(InteractiveService interactivity, IDataCache cache)
    {
        _interactivity = interactivity;
        this.cache = cache;
    }

    /// <summary>
    ///     Creates a new ticket panel.
    /// </summary>
    /// <param name="channel">The channel to create the panel in.</param>
    [SlashCommand("createpanel", "Creates a new ticket panel")]
    [SlashUserPerm(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    public Task CreatePanel(
        [Summary("channel", "Channel to create the panel in")]
        ITextChannel channel
    )
    {
        return RespondWithModalAsync<PanelCreationModal>($"create_panel:{channel.Id}");
    }

    /// <summary>
    ///     Lists all components of a specific ticket panel
    /// </summary>
    /// <remarks>
    ///     This command provides detailed information about all components on a panel, including:
    ///     - Button and select menu IDs
    ///     - Component configurations
    ///     - Associated categories and roles
    ///     - Modal and custom message settings
    /// </remarks>
    /// <param name="panelId">The message ID of the panel to list components from</param>
    [SlashCommand("listpanel", "Lists all components of a ticket panel")]
    [RequireContext(ContextType.Guild)]
    public async Task ListPanel(
        [Summary("panel-id", "Message ID of the panel to list")]
        ulong panelId)
    {
        try
        {
            var buttons = await Service.GetPanelButtonsAsync(panelId);
            var menus = await Service.GetPanelSelectMenusAsync(panelId);

            var embed = new EmbedBuilder()
                .WithTitle("Panel Components")
                .WithOkColor();

            if (buttons.Any())
            {
                var buttonText = new StringBuilder();
                foreach (var button in buttons)
                {
                    buttonText.AppendLine($"**Button ID: {button.Id}**")
                        .AppendLine($"└ Label: {button.Label}")
                        .AppendLine($"└ Style: {button.Style}")
                        .AppendLine($"└ Custom ID: {button.CustomId}")
                        .AppendLine($"└ Has Modal: {(button.HasModal ? "Yes" : "No")}")
                        .AppendLine($"└ Has Custom Open Message: {(button.HasCustomOpenMessage ? "Yes" : "No")}")
                        .AppendLine($"└ Category: {(button.CategoryId.HasValue ? $"<#{button.CategoryId}>" : "None")}")
                        .AppendLine(
                            $"└ Archive Category: {(button.ArchiveCategoryId.HasValue ? $"<#{button.ArchiveCategoryId}>" : "None")}")
                        .AppendLine(
                            $"└ Support Roles: {string.Join(", ", button.SupportRoles.Select(r => $"<@&{r}>"))}")
                        .AppendLine($"└ Viewer Roles: {string.Join(", ", button.ViewerRoles.Select(r => $"<@&{r}>"))}")
                        .AppendLine();
                }

                embed.AddField("Buttons", buttonText.ToString());
            }

            if (menus.Any())
            {
                var menuText = new StringBuilder();
                foreach (var menu in menus)
                {
                    menuText.AppendLine($"**Menu ID: {menu.Id}**")
                        .AppendLine($"└ Custom ID: {menu.CustomId}")
                        .AppendLine($"└ Placeholder: {menu.Placeholder}")
                        .AppendLine("└ Options:");

                    foreach (var option in menu.Options)
                    {
                        menuText.AppendLine($"  **Option ID: {option.Id}**")
                            .AppendLine($"  └ Label: {option.Label}")
                            .AppendLine($"  └ Value: {option.Value}")
                            .AppendLine($"  └ Description: {option.Description}")
                            .AppendLine($"  └ Has Modal: {(option.HasModal ? "Yes" : "No")}")
                            .AppendLine($"  └ Has Custom Open Message: {(option.HasCustomOpenMessage ? "Yes" : "No")}")
                            .AppendLine(
                                $"  └ Category: {(option.CategoryId.HasValue ? $"<#{option.CategoryId}>" : "None")}")
                            .AppendLine(
                                $"  └ Archive Category: {(option.ArchiveCategoryId.HasValue ? $"<#{option.ArchiveCategoryId}>" : "None")}");
                    }

                    menuText.AppendLine();
                }

                embed.AddField("Select Menus", menuText.ToString());
            }

            if (!buttons.Any() && !menus.Any())
            {
                embed.WithDescription("No components found on this panel.");
            }

            await RespondAsync(embed: embed.Build());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error listing panel components for panel {PanelId}", panelId);
            await RespondAsync("An error occurred while listing panel components.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Lists all ticket panels in the guild
    /// </summary>
    /// <remarks>
    ///     This command displays paginated information about all ticket panels in the server, including:
    ///     - Channel locations
    ///     - Message IDs
    ///     - Component configurations
    ///     - Associated categories and roles
    ///     Each panel's information is shown on its own page for easy navigation.
    /// </remarks>
    [SlashCommand("listpanels", "Lists all ticket panels in the server")]
    [RequireContext(ContextType.Guild)]
    public async Task ListPanels()
    {
        try
        {
            var panels = await Service.GetAllPanelsAsync(Context.Guild.Id);

            if (!panels.Any())
            {
                await RespondAsync("No ticket panels found in this server.", ephemeral: true);
                return;
            }

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(panels.Count / 5)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await _interactivity.SendPaginatorAsync(paginator, Context.Interaction, TimeSpan.FromMinutes(60));

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask;
                var pagePanels = panels.Skip(5 * page).Take(5);
                var pageBuilder = new PageBuilder()
                    .WithTitle("Ticket Panels")
                    .WithOkColor();

                foreach (var panel in pagePanels)
                {
                    var channel = await Context.Guild.GetChannelAsync(panel.ChannelId) as ITextChannel;
                    var fieldBuilder = new StringBuilder();

                    fieldBuilder.AppendLine($"Channel: #{channel?.Name ?? "deleted-channel"}");

                    if (panel.Buttons.Any())
                    {
                        fieldBuilder.AppendLine("\n**Buttons:**");
                        foreach (var button in panel.Buttons)
                        {
                            fieldBuilder.AppendLine($"• ID: {button.Id} | Label: {button.Label}")
                                .AppendLine($"  Style: {button.Style}")
                                .AppendLine(
                                    $"  Category: {(button.CategoryId.HasValue ? $"<#{button.CategoryId}>" : "None")}")
                                .AppendLine(
                                    $"  Support Roles: {string.Join(", ", button.SupportRoles.Select(r => $"<@&{r}>"))}");
                        }
                    }

                    if (panel.SelectMenus.Any())
                    {
                        fieldBuilder.AppendLine("\n**Select Menus:**");
                        foreach (var menu in panel.SelectMenus)
                        {
                            fieldBuilder.AppendLine($"• ID: {menu.Id} | Options: {menu.Options.Count}");
                            foreach (var option in menu.Options)
                            {
                                fieldBuilder.AppendLine($"  - Option ID: {option.Id} | Label: {option.Label}");
                            }
                        }
                    }

                    pageBuilder.AddField($"Panel ID: {panel.MessageId}", fieldBuilder.ToString());
                }

                return pageBuilder;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error listing panels");
            await RespondAsync("An error occurred while listing panels.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Deletes a ticket panel.
    /// </summary>
    /// <param name="panelId">The ID of the panel to delete.</param>
    [SlashCommand("deletepanel", "Deletes a ticket panel")]
    [SlashUserPerm(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    public async Task DeletePanel(
        [Summary("panel-id", "Message ID of the panel to delete")]
        ulong panelId)
    {
        try
        {
            await Service.DeletePanelAsync(panelId, ctx.Guild);
            await RespondAsync("Panel deleted successfully!", ephemeral: true);
        }
        catch (InvalidOperationException ex)
        {
            await RespondAsync($"Error: {ex.Message}", ephemeral: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error deleting panel {PanelId}", panelId);
            await RespondAsync("An error occurred while deleting the panel.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Claims ownership of a ticket as a staff member.
    /// </summary>
    /// <param name="channel">Optional channel to claim. If not provided, uses the current channel.</param>
    /// <remarks>
    ///     Staff members can use this command to claim responsibility for a ticket.
    ///     This shows other staff members who is handling the ticket.
    /// </remarks>
    [SlashCommand("claim", "Claims a ticket")]
    [RequireContext(ContextType.Guild)]
    public async Task ClaimTicket(
        [Summary("channel", "The ticket channel to claim")]
        ITextChannel? channel = null
    )
    {
        channel ??= ctx.Channel as ITextChannel;
        if (channel == null)
        {
            await RespondAsync("This command must be used in a text channel.", ephemeral: true);
            return;
        }

        try
        {
            var success = await Service.ClaimTicket(ctx.Guild, channel.Id, ctx.User as IGuildUser);
            if (success)
                await RespondAsync("Ticket claimed successfully!", ephemeral: true);
            else
                await RespondAsync("Failed to claim ticket. It may already be claimed or closed.", ephemeral: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error claiming ticket in channel {ChannelId}", channel.Id);
            await RespondAsync("An error occurred while claiming the ticket.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Handles the close button interaction for tickets.
    /// </summary>
    /// <remarks>
    ///     This method is called when a user clicks the close button on a ticket.
    ///     It will close the ticket and notify the user of the result.
    ///     The button uses the custom ID "ticket_close".
    /// </remarks>
    [ComponentInteraction("ticket_close", true)]
    public async Task HandleTicketClose()
    {
        try
        {
            var success = await Service.CloseTicket(ctx.Guild, ctx.Channel.Id);
            if (success)
                await RespondAsync("Ticket closed successfully!", ephemeral: true);
            else
                await RespondAsync("Failed to close ticket. It may already be closed.", ephemeral: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error closing ticket in channel {ChannelId}", ctx.Channel.Id);
            await RespondAsync("An error occurred while closing the ticket.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Closes a ticket channel.
    /// </summary>
    /// <param name="channel">Optional channel to close. If not provided, uses the current channel.</param>
    /// <remarks>
    ///     This command allows staff to close tickets either in the current channel
    ///     or in a specified channel. Closed tickets may be moved to an archive category
    ///     if one is configured.
    /// </remarks>
    [SlashCommand("close", "Closes a ticket")]
    [RequireContext(ContextType.Guild)]
    public async Task CloseTicket(
        [Summary("channel", "The ticket channel to close")]
        ITextChannel? channel = null
    )
    {
        channel ??= ctx.Channel as ITextChannel;
        if (channel == null)
        {
            await RespondAsync("This command must be used in a text channel.", ephemeral: true);
            return;
        }

        try
        {
            var success = await Service.CloseTicket(ctx.Guild, channel.Id);
            if (success)
                await RespondAsync("Ticket closed successfully!", ephemeral: true);
            else
                await RespondAsync("Failed to close ticket. It may already be closed.", ephemeral: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error closing ticket in channel {ChannelId}", channel.Id);
            await RespondAsync("An error occurred while closing the ticket.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Removes a staff member's claim from a ticket.
    /// </summary>
    /// <param name="ticketId">The ID of the ticket to unclaim.</param>
    [SlashCommand("unclaim", "Removes a staff member's claim from a ticket")]
    [RequireContext(ContextType.Guild)]
    public async Task UnclaimTicket(
        [Summary("ticket-id", "ID of the ticket to unclaim")]
        int ticketId)
    {
        try
        {
            var ticket = await Service.GetTicketAsync(ticketId);
            if (ticket == null)
            {
                await RespondAsync("Ticket not found!", ephemeral: true);
                return;
            }

            // Verify permissions
            var guildUser = ctx.User as IGuildUser;
            var channel = await ctx.Guild.GetChannelAsync(ticket.ChannelId) as ITextChannel;

            if (channel == null)
            {
                await RespondAsync("Ticket channel not found!", ephemeral: true);
                return;
            }

            var permissions = guildUser.GetPermissions(channel);
            if (!permissions.ManageChannel && !guildUser.GuildPermissions.Administrator)
            {
                await RespondAsync("You don't have permission to unclaim tickets!", ephemeral: true);
                return;
            }

            await Service.UnclaimTicketAsync(ticket, guildUser);
            await RespondAsync("Ticket unclaimed successfully!", ephemeral: true);
        }
        catch (InvalidOperationException ex)
        {
            await RespondAsync($"Error: {ex.Message}", ephemeral: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error unclaiming ticket {TicketId}", ticketId);
            await RespondAsync("An error occurred while unclaiming the ticket.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Adds a note to a ticket.
    /// </summary>
    /// <param name="ticket">The ID of the ticket.</param>
    [SlashCommand("note", "Adds a note to a ticket")]
    [RequireContext(ContextType.Guild)]
    public Task AddNote(
        [Summary("ticket", "The ticket to add a note to")]
        int ticket
    )
    {
        return RespondWithModalAsync<TicketNoteModal>($"ticket_note:{ticket}");
    }

    /// <summary>
    ///     Archives a ticket.
    /// </summary>
    /// <param name="ticket">The ID of the ticket to archive.</param>
    [SlashCommand("archive", "Archives a ticket")]
    [RequireContext(ContextType.Guild)]
    public async Task ArchiveTicket(
        [Summary("ticket", "The ticket to archive")]
        int ticket
    )
    {
        var ticketObj = await Service.GetTicketAsync(ticket);
        if (ticketObj == null)
        {
            await RespondAsync("Ticket not found!", ephemeral: true);
            return;
        }

        await Service.ArchiveTicketAsync(ticketObj);
        await RespondAsync("Ticket archived successfully!", ephemeral: true);
    }

    /// <summary>
    ///     Sets a ticket's priority.
    /// </summary>
    /// <param name="ticket">The ID of the ticket.</param>
    [SlashCommand("priority", "Sets a ticket's priority")]
    [RequireContext(ContextType.Guild)]
    public Task SetPriority(
        [Summary("ticket", "The ticket to set priority for")]
        int ticket
    )
    {
        return RespondWithModalAsync<TicketPriorityModal>($"ticket_priority:{ticket}");
    }

    /// <summary>
    ///     Group for managing ticket panels.
    /// </summary>
    [Group("panel", "Manage ticket panels")]
    public class PanelCommands : MewdekoSlashModuleBase<TicketService>
    {
        private readonly IDataCache cache;

        /// <summary>
        ///     Initializes a new instance of the <see cref="PanelCommands" /> class.
        /// </summary>
        /// <param name="cache">The cache service.</param>
        public PanelCommands(IDataCache cache)
        {
            this.cache = cache;
        }

        /// <summary>
        ///     Adds a button to a panel.
        /// </summary>
        /// <param name="panelId">The ID of the panel.</param>
        [SlashCommand("addbutton", "Adds a button to a panel")]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AddButton(
            [Summary("panel", "The panel to add a button to")]
            string panelId
        )
        {
            var components = new ComponentBuilder()
                .WithButton("Primary", $"btn_style:{panelId}:primary")
                .WithButton("Success", $"btn_style:{panelId}:success", ButtonStyle.Success)
                .WithButton("Secondary", $"btn_style:{panelId}:secondary", ButtonStyle.Secondary)
                .WithButton("Danger", $"btn_style:{panelId}:danger", ButtonStyle.Danger);

            await RespondAsync("Choose the button style:", components: components.Build());
        }

        /// <summary>
        ///     Adds a select menu to a panel.
        /// </summary>
        /// <param name="panelId">The ID of the panel.</param>
        [SlashCommand("addmenu", "Adds a select menu to a panel")]
        [SlashUserPerm(GuildPermission.Administrator)]
        public Task AddSelectMenu(
            [Summary("panel", "The panel to add a menu to")]
            string panelId
        )
        {
            return RespondWithModalAsync<SelectMenuCreationModal>($"create_menu:{panelId}");
        }
    }

    /// <summary>
    ///     Group for managing ticket settings.
    /// </summary>
    [Group("settings", "Manage ticket settings")]
    public class SettingsCommands : MewdekoSlashModuleBase<TicketService>
    {
        /// <summary>
        ///     Sets the transcript channel.
        /// </summary>
        /// <param name="channel">The channel for ticket transcripts.</param>
        [SlashCommand("transcripts", "Sets the transcript channel")]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task SetTranscriptChannel(
            [Summary("channel", "Channel for ticket transcripts")]
            ITextChannel channel
        )
        {
            await Service.SetTranscriptChannelAsync(ctx.Guild.Id, channel.Id);
            await RespondAsync($"Transcript channel set to {channel.Mention}");
        }

        /// <summary>
        ///     Sets the log channel.
        /// </summary>
        /// <param name="channel">The channel for ticket logs.</param>
        [SlashCommand("logs", "Sets the log channel")]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task SetLogChannel(
            [Summary("channel", "Channel for ticket logs")]
            ITextChannel channel
        )
        {
            await Service.SetLogChannelAsync(ctx.Guild.Id, channel.Id);
            await RespondAsync($"Log channel set to {channel.Mention}");
        }
    }

    /// <summary>
    ///     Provides commands for managing ticket cases and their relationships with tickets.
    /// </summary>
    [Group("cases", "Manage ticket cases")]
    public class CaseManagementCommands : MewdekoSlashModuleBase<TicketService>
    {
        private readonly InteractiveService _interactivity;

        /// <summary>
        ///     Initializes a new instance of the <see cref="CaseManagementCommands" /> class.
        /// </summary>
        /// <param name="interactivity">The interactive service for handling paginated responses.</param>
        public CaseManagementCommands(InteractiveService interactivity)
        {
            _interactivity = interactivity;
        }

        /// <summary>
        ///     Creates a new case for organizing tickets.
        /// </summary>
        [SlashCommand("create", "Creates a new case")]
        [RequireContext(ContextType.Guild)]
        public Task CreateCase()
        {
            return RespondWithModalAsync<CaseCreationModal>("create_case");
        }

        /// <summary>
        ///     Views details of a specific case or lists all cases if no ID is provided.
        /// </summary>
        /// <param name="caseId">Optional ID of the specific case to view. If not provided, lists all cases.</param>
        [SlashCommand("view", "Views case details")]
        [RequireContext(ContextType.Guild)]
        public async Task ViewCase(
            [Summary("case-id", "ID of the case to view")]
            int? caseId = null)
        {
            if (caseId.HasValue)
            {
                var ticketCase = await Service.GetCaseAsync(caseId.Value);
                if (ticketCase == null)
                {
                    await RespondAsync("Case not found!", ephemeral: true);
                    return;
                }

                var creator = await ctx.Guild.GetUserAsync(ticketCase.CreatedBy);
                var eb = new EmbedBuilder()
                    .WithTitle($"Case #{ticketCase.Id}: {ticketCase.Title}")
                    .WithDescription(ticketCase.Description)
                    .AddField("Created By", creator?.Mention ?? "Unknown", true)
                    .AddField("Created At", ticketCase.CreatedAt.ToString("g"), true)
                    .AddField("Status", ticketCase.ClosedAt.HasValue ? "Closed" : "Open", true)
                    .WithOkColor();

                if (ticketCase.LinkedTickets.Any())
                {
                    eb.AddField("Linked Tickets",
                        string.Join("\n", ticketCase.LinkedTickets.Select(t => $"#{t.Id}")));
                }

                if (ticketCase.Notes.Any())
                {
                    eb.AddField("Notes",
                        string.Join("\n\n", ticketCase.Notes
                            .OrderByDescending(n => n.CreatedAt)
                            .Take(5)
                            .Select(n => $"{n.Content}\n- <@{n.AuthorId}> at {n.CreatedAt:g}")));
                }

                await RespondAsync(embed: eb.Build());
            }
            else
            {
                await ListAllCases();
            }
        }

        /// <summary>
        ///     Links one or more tickets to an existing case.
        /// </summary>
        /// <param name="caseId">The ID of the case to link tickets to.</param>
        /// <param name="ticketIds">Comma-separated list of ticket IDs to link to the case.</param>
        [SlashCommand("link", "Links tickets to a case")]
        [RequireContext(ContextType.Guild)]
        public async Task LinkTickets(
            [Summary("case", "The case to link tickets to")]
            int caseId,
            [Summary("tickets", "Comma-separated list of ticket IDs")]
            string ticketIds)
        {
            var ticketCase = await Service.GetCaseAsync(caseId);
            if (ticketCase == null)
            {
                await RespondAsync("Case not found!", ephemeral: true);
                return;
            }

            var tickets = await Service.GetTicketsAsync(ticketIds.Split(',').Select(int.Parse));
            await Service.LinkTicketsToCase(caseId, tickets);
            await RespondAsync($"Successfully linked {tickets.Count} ticket(s) to case #{caseId}");
        }

        /// <summary>
        ///     Unlinks one or more tickets from their associated case.
        /// </summary>
        /// <param name="caseId">The ID of the case to unlink tickets from.</param>
        /// <param name="ticketIds">Comma-separated list of ticket IDs to unlink.</param>
        [SlashCommand("unlink", "Unlinks tickets from a case")]
        [RequireContext(ContextType.Guild)]
        public async Task UnlinkTickets(
            [Summary("case", "The case to unlink tickets from")]
            int caseId,
            [Summary("tickets", "Comma-separated list of ticket IDs")]
            string ticketIds)
        {
            var ticketCase = await Service.GetCaseAsync(caseId);
            if (ticketCase == null)
            {
                await RespondAsync("Case not found!", ephemeral: true);
                return;
            }

            var tickets = await Service.GetTicketsAsync(ticketIds.Split(',').Select(int.Parse));
            await Service.UnlinkTicketsFromCase(tickets);
            await RespondAsync($"Successfully unlinked {tickets.Count} ticket(s) from case #{caseId}");
        }

        /// <summary>
        ///     Adds a note to an existing case.
        /// </summary>
        /// <param name="caseId">The ID of the case to add a note to.</param>
        [SlashCommand("note", "Adds a note to a case")]
        [RequireContext(ContextType.Guild)]
        public Task AddNote(
            [Summary("case-id", "ID of the case")] int caseId)
        {
            return RespondWithModalAsync<CaseNoteModal>($"case_note:{caseId}");
        }

        /// <summary>
        ///     Closes an open case.
        /// </summary>
        /// <param name="caseId">The ID of the case to close.</param>
        [SlashCommand("close", "Closes a case")]
        [RequireContext(ContextType.Guild)]
        public async Task CloseCase(
            [Summary("case-id", "ID of the case to close")]
            int caseId)
        {
            var ticketCase = await Service.GetCaseAsync(caseId);
            if (ticketCase == null)
            {
                await RespondAsync("Case not found!", ephemeral: true);
                return;
            }

            if (ticketCase.ClosedAt.HasValue)
            {
                await RespondAsync("This case is already closed!", ephemeral: true);
                return;
            }

            await Service.CloseCaseAsync(ticketCase);
            await RespondAsync($"Case #{caseId} closed successfully!");
        }

        /// <summary>
        ///     Reopens a previously closed case.
        /// </summary>
        /// <param name="caseId">The ID of the case to reopen.</param>
        [SlashCommand("reopen", "Reopens a closed case")]
        [RequireContext(ContextType.Guild)]
        public async Task ReopenCase(
            [Summary("case-id", "ID of the case to reopen")]
            int caseId)
        {
            var ticketCase = await Service.GetCaseAsync(caseId);
            if (ticketCase == null)
            {
                await RespondAsync("Case not found!", ephemeral: true);
                return;
            }

            if (!ticketCase.ClosedAt.HasValue)
            {
                await RespondAsync("This case is already open!", ephemeral: true);
                return;
            }

            await Service.ReopenCaseAsync(ticketCase);
            await RespondAsync($"Case #{caseId} reopened successfully!");
        }

        /// <summary>
        ///     Updates the title and/or description of an existing case.
        /// </summary>
        /// <param name="caseId">The ID of the case to update.</param>
        [SlashCommand("update", "Updates a case's details")]
        [RequireContext(ContextType.Guild)]
        public Task UpdateCase(
            [Summary("case-id", "ID of the case to update")]
            int caseId)
        {
            return RespondWithModalAsync<CaseUpdateModal>($"case_update:{caseId}");
        }

        /// <summary>
        ///     Displays a paginated list of all cases in the guild.
        /// </summary>
        private async Task ListAllCases()
        {
            var cases = await Service.GetGuildCasesAsync(ctx.Guild.Id);
            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(cases.Count / 10)
                .WithDefaultEmotes()
                .Build();

            await _interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60));

            async Task<PageBuilder> PageFactory(int page)
            {
                var pageBuilder = new PageBuilder()
                    .WithTitle("Cases")
                    .WithOkColor();

                foreach (var ticketCase in cases.Skip(page * 10).Take(10))
                {
                    var creator = await ctx.Guild.GetUserAsync(ticketCase.CreatedBy);
                    pageBuilder.AddField($"Case #{ticketCase.Id}: {ticketCase.Title}",
                        $"Created by: {creator?.Mention ?? "Unknown"}\n" +
                        $"Status: {(ticketCase.ClosedAt.HasValue ? "Closed" : "Open")}\n" +
                        $"Linked Tickets: {ticketCase.LinkedTickets.Count}\n" +
                        $"Notes: {ticketCase.Notes.Count}");
                }

                return pageBuilder;
            }
        }
    }


    #region Modal Handlers

    /// <summary>
    ///     Handles the submission of case notes through the modal.
    /// </summary>
    /// <param name="caseId">The ID of the case being noted.</param>
    /// <param name="modal">The modal containing the note content.</param>
    [ModalInteraction("case_note:*", true)]
    public async Task HandleCaseNote(string caseId, CaseNoteModal modal)
    {
        var note = await Service.AddCaseNoteAsync(
            int.Parse(caseId),
            ctx.User.Id,
            modal.Content);

        if (note != null)
            await RespondAsync("Note added successfully!", ephemeral: true);
        else
            await RespondAsync("Failed to add note. The case may not exist.", ephemeral: true);
    }

    /// <summary>
    ///     Handles panel creation modal submission.
    /// </summary>
    [ModalInteraction("create_panel:*", true)]
    public async Task HandlePanelCreation(string channelId, PanelCreationModal modal)
    {
        var channel = await ctx.Guild.GetTextChannelAsync(ulong.Parse(channelId));
        var panel = await Service.CreatePanelAsync(channel, modal.EmbedJson);
        await RespondAsync("Panel created successfully!", ephemeral: true);
    }

    /// <summary>
    ///     Handles note modal submission.
    /// </summary>
    [ModalInteraction("ticket_note:*", true)]
    public async Task HandleTicketNote(string ticketId, TicketNoteModal modal)
    {
        var ticket = await Service.GetTicketAsync(int.Parse(ticketId));
        await Service.AddNoteAsync(ticket, ctx.User as IGuildUser, modal.Content);
        await RespondAsync("Note added successfully!", ephemeral: true);
    }

    /// <summary>
    ///     Handles priority modal submission.
    /// </summary>
    [ModalInteraction("ticket_priority:*", true)]
    public async Task HandleTicketPriority(string ticketId, TicketPriorityModal modal)
    {
        var ticket = await Service.GetTicketAsync(int.Parse(ticketId));
        await Service.SetTicketPriorityAsync(ticket, modal.Priority, ctx.User as IGuildUser);
        await RespondAsync($"Priority set to {modal.Priority}!", ephemeral: true);
    }

    #endregion

    #region Button Style Handlers

    /// <summary>
    ///     Starts the button creation process.
    /// </summary>
    [SlashCommand("addbutton", "Add a button to a ticket panel")]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task AddButton(
        [Summary("panel-id")] string panelId)
    {
        var components = new ComponentBuilder()
            .WithButton("Primary", $"btn_style:{panelId}:primary")
            .WithButton("Success", $"btn_style:{panelId}:success", ButtonStyle.Success)
            .WithButton("Secondary", $"btn_style:{panelId}:secondary", ButtonStyle.Secondary)
            .WithButton("Danger", $"btn_style:{panelId}:danger", ButtonStyle.Danger);

        await RespondAsync("Choose the button style:", components: components.Build());
    }

    /// <summary>
    ///     Handles button style selection.
    /// </summary>
    [ComponentInteraction("btn_style:*:*", true)]
    public async Task HandleButtonStyle(string panelId, string style)
    {
        try
        {
            // Store the selected style
            await cache.Redis.GetDatabase().StringSetAsync($"btn_creation:{ctx.User.Id}:style", style);

            // Ask for label using NextMessageAsync
            await ctx.Interaction.SendConfirmAsync(Strings.EnterButtonLabel(ctx.Guild.Id));
            var label = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);

            if (string.IsNullOrEmpty(label))
            {
                await ctx.Interaction.SendErrorAsync(Strings.ButtonCreationNoLabel(ctx.Guild.Id), Config);
                return;
            }

            await cache.Redis.GetDatabase().StringSetAsync($"btn_creation:{ctx.User.Id}:label", label);

            // Ask about emoji
            var components = new ComponentBuilder()
                .WithButton("Yes", $"btn_emoji:{panelId}:yes")
                .WithButton("No", $"btn_emoji:{panelId}:no")
                .Build();

            await ctx.Interaction.FollowupAsync(
                embed: new EmbedBuilder().WithDescription("Would you like to add an emoji to the button?").WithOkColor()
                    .Build(), components: components);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error while adding button style");
        }
    }

    /// <summary>
    ///     Handles label input.
    /// </summary>
    [ModalInteraction("btn_label:*", true)]
    public async Task HandleButtonLabel(string panelId, SimpleInputModal modal)
    {
        // Store the label
        await cache.Redis.GetDatabase().StringSetAsync($"btn_creation:{ctx.User.Id}:label", modal.Input);

        // Ask if they want an emoji
        var components = new ComponentBuilder()
            .WithButton("Yes", $"btn_emoji:{panelId}:yes")
            .WithButton("No", $"btn_emoji:{panelId}:no");

        await RespondAsync("Would you like to add an emoji to the button?", components: components.Build());
    }

    /// <summary>
    ///     Handles emoji choice.
    /// </summary>
    [ComponentInteraction("btn_emoji:*:*", true)]
    public async Task HandleEmojiChoice(string panelId, string choice)
    {
        if (choice == "yes")
        {
            await ctx.Interaction.SendConfirmAsync(Strings.EnterEmoji(ctx.Guild.Id));
            var emoji = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);

            if (!string.IsNullOrEmpty(emoji))
            {
                await cache.Redis.GetDatabase().StringSetAsync($"btn_creation:{ctx.User.Id}:emoji", emoji);
            }
        }

        await PromptTicketSettings(panelId);
    }

    /// <summary>
    ///     Handles ticket button clicks and shows modal if configured
    /// </summary>
    [ComponentInteraction("ticket_btn_*", true)]
    [RequireContext(ContextType.Guild)]
    public async Task HandleTicketButton(string button)
    {
        try
        {
            var panelButton = await Service.GetButtonAsync($"ticket_btn_{button}");
            if (panelButton == null)
            {
                await RespondAsync("This ticket type is no longer available.", ephemeral: true);
                return;
            }

            if (!string.IsNullOrEmpty(panelButton.ModalJson))
            {
                await Service.HandleModalCreation(
                    ctx.User as IGuildUser,
                    panelButton.ModalJson,
                    $"ticket_modal:{panelButton.Id}",
                    ctx.Interaction
                );
            }
            else
            {
                await Service.CreateTicketAsync(
                    ctx.Guild,
                    ctx.User,
                    panelButton
                );
                await RespondAsync("Ticket created successfully!", ephemeral: true);
            }
        }
        catch (InvalidOperationException ex)
        {
            await RespondAsync(ex.Message, ephemeral: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling ticket button");
            await RespondAsync("An error occurred while creating your ticket.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Handles emoji input.
    /// </summary>
    [ModalInteraction("btn_emoji_input:*", true)]
    public async Task HandleEmojiInput(string panelId, SimpleInputModal modal)
    {
        await cache.Redis.GetDatabase().StringSetAsync($"btn_creation:{ctx.User.Id}:emoji", modal.Input);
        await PromptTicketSettings(panelId);
    }

    private async Task PromptTicketSettings(string panelId)
    {
        // Get guild categories
        var categories = await ctx.Guild.GetCategoriesAsync();

        var menuBuilder = new SelectMenuBuilder()
            .WithPlaceholder("Select Categories")
            .WithCustomId($"btn_category:{panelId}")
            .WithMinValues(0)
            .WithMaxValues(2);

        // Add all categories as options
        foreach (var category in categories)
        {
            menuBuilder.AddOption(
                $"Create: {category.Name}", // Label
                $"create:{category.Id}", // Value includes type and ID
                "Category for new tickets"
            );
            menuBuilder.AddOption(
                $"Archive: {category.Name}", // Label
                $"archive:{category.Id}", // Value includes type and ID
                "Category for archived tickets"
            );
        }

        var components = new ComponentBuilder()
            .WithSelectMenu(menuBuilder)
            .WithButton("Skip", $"btn_category:{panelId}:skip")
            .Build();

        await FollowupAsync("Select ticket categories (optional):", components: components);
    }

    /// <summary>
    ///     Handles category selection.
    /// </summary>
    [ComponentInteraction("btn_category:*", true)]
    public async Task HandleCategorySelect(string panelId, string[] values)
    {
        try
        {
            await DeferAsync();
            ulong? createCategory = null;
            ulong? archiveCategory = null;

            foreach (var selection in values)
            {
                var parts = selection.Split(':');
                var id = ulong.Parse(parts[1]);
                if (parts[0] == "create")
                    createCategory = id;
                else if (parts[0] == "archive")
                    archiveCategory = id;
            }

            await cache.Redis.GetDatabase()
                .StringSetAsync($"btn_creation:{ctx.User.Id}:category", createCategory?.ToString());
            await cache.Redis.GetDatabase()
                .StringSetAsync($"btn_creation:{ctx.User.Id}:archive_category", archiveCategory?.ToString());

            var roleMenuBuilder = new SelectMenuBuilder()
                .WithPlaceholder("Select Support Roles")
                .WithCustomId($"btn_roles:{panelId}")
                .WithMinValues(0);

            foreach (var role in ctx.Guild.Roles.Where(r => r.Permissions.ManageChannels))
            {
                roleMenuBuilder.AddOption(role.Name, role.Id.ToString(), $"Support role: {role.Name}");
            }

            var components = new ComponentBuilder()
                .WithSelectMenu(roleMenuBuilder)
                .WithButton("Continue", $"btn_roles:{panelId}:done")
                .Build();

            await FollowupAsync("Select support roles (optional):", components: components);
        }
        catch (Exception e)
        {
            Log.Error(e, "OOPSIE");
        }
    }

    /// <summary>
    ///     Handles role selection.
    /// </summary>
    [ComponentInteraction("btn_roles:*", true)]
    public async Task HandleRoleSelect(string panelId, string[] values)
    {
        try
        {
            if (values.Any())
            {
                await cache.Redis.GetDatabase()
                    .StringSetAsync($"btn_creation:{ctx.User.Id}:roles", JsonSerializer.Serialize(values));
            }

            var autoCloseMenu = new SelectMenuBuilder()
                .WithPlaceholder("Select Auto-Close Time")
                .WithCustomId($"btn_autoclose:{panelId}")
                .WithMinValues(1)
                .WithMaxValues(1)
                .AddOption("1 hour", "1", "Auto-close after 1 hour of inactivity")
                .AddOption("12 hours", "12", "Auto-close after 12 hours of inactivity")
                .AddOption("24 hours", "24", "Auto-close after 24 hours of inactivity")
                .AddOption("48 hours", "48", "Auto-close after 48 hours of inactivity")
                .AddOption("1 week", "168", "Auto-close after 1 week of inactivity");

            var components = new ComponentBuilder()
                .WithSelectMenu(autoCloseMenu)
                .WithButton("Skip", $"btn_autoclose:{panelId}:skip")
                .Build();

            await RespondAsync("Select auto-close time (optional):", components: components);
        }
        catch (Exception e)
        {
            Log.Error(e, "OOPSIE");
        }
    }

    /// <summary>
    ///     Handles the completion of support role selection and prompts for viewer roles.
    /// </summary>
    /// <param name="panelId">The ID of the panel being configured.</param>
    /// <remarks>
    ///     This method is called after support roles have been selected. It presents a selection menu
    ///     for viewer roles, which are roles that can view but not interact with tickets.
    /// </remarks>
    [ComponentInteraction("btn_roles:*:done", true)]
    public async Task HandleRoleDone(string panelId)
    {
        var viewerRoleMenu = new SelectMenuBuilder()
            .WithPlaceholder("Select Viewer Roles")
            .WithCustomId($"btn_viewer_roles:{panelId}")
            .WithMinValues(0);
        foreach (var role in ctx.Guild.Roles)
        {
            viewerRoleMenu.AddOption(role.Name, role.Id.ToString(), $"Viewer role: {role.Name}");
        }

        var components = new ComponentBuilder()
            .WithSelectMenu(viewerRoleMenu)
            .WithButton("Skip", $"btn_viewer_roles:{panelId}:skip")
            .Build();
        await RespondAsync("Select viewer roles (optional):", components: components);
    }

    /// <summary>
    ///     Handles viewer role selection and additional ticket configuration settings.
    /// </summary>
    /// <param name="panelId">The ID of the panel being configured.</param>
    /// <param name="values">The selected viewer role IDs.</param>
    /// <remarks>
    ///     This method processes selected viewer roles and sequentially prompts for:
    ///     - Ticket opening message
    ///     - Modal JSON configuration
    ///     - Allowed priorities
    ///     - Default priority
    ///     - Required response time
    ///     Each prompt allows skipping via 'skip' message.
    /// </remarks>
    [ComponentInteraction("btn_viewer_roles:*", true)]
    public async Task HandleViewerRoleSelect(string panelId, string[] values)
    {
        if (values.Any())
        {
            await cache.Redis.GetDatabase()
                .StringSetAsync($"btn_creation:{ctx.User.Id}:viewer_roles", JsonSerializer.Serialize(values));
        }

        await ctx.Interaction.SendConfirmAsync(Strings.EnterTicketOpening(ctx.Guild.Id));

        var openMsg = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
        if (!string.IsNullOrEmpty(openMsg) && openMsg.ToLower() != "skip")
        {
            await cache.Redis.GetDatabase()
                .StringSetAsync($"btn_creation:{ctx.User.Id}:open_message", openMsg);
        }

        await ctx.Interaction.SendConfirmAsync(Strings.EnterModalJson(ctx.Guild.Id));

        var modalJson = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
        if (!string.IsNullOrEmpty(modalJson) && modalJson.ToLower() != "skip")
        {
            await cache.Redis.GetDatabase()
                .StringSetAsync($"btn_creation:{ctx.User.Id}:modal_json", modalJson);
        }

        await ctx.Interaction.SendConfirmAsync(
            "Please enter the allowed priorities, comma separated (or type 'skip' to skip):");

        var priorities = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
        if (!string.IsNullOrEmpty(priorities) && priorities.ToLower() != "skip")
        {
            await cache.Redis.GetDatabase()
                .StringSetAsync($"btn_creation:{ctx.User.Id}:priorities",
                    JsonSerializer.Serialize(priorities.Split(',').Select(p => p.Trim())));
            await ctx.Interaction.SendConfirmAsync(Strings.EnterPriority(ctx.Guild.Id));
            var defaultPriority = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
            if (!string.IsNullOrEmpty(defaultPriority) && defaultPriority.ToLower() != "skip")
            {
                await cache.Redis.GetDatabase()
                    .StringSetAsync($"btn_creation:{ctx.User.Id}:default_priority", defaultPriority);
            }
        }

        var responseTimeMenu = new SelectMenuBuilder()
            .WithPlaceholder("Select Response Time")
            .WithCustomId($"btn_response_time:{panelId}")
            .WithMinValues(0)
            .WithMaxValues(1)
            .AddOption("1 hour", "1", "Response required within 1 hour")
            .AddOption("4 hours", "4", "Response required within 4 hours")
            .AddOption("12 hours", "12", "Response required within 12 hours")
            .AddOption("24 hours", "24", "Response required within 24 hours");
        var components = new ComponentBuilder()
            .WithSelectMenu(responseTimeMenu)
            .WithButton("Skip", $"btn_response_time:{panelId}:skip")
            .Build();
        await ctx.Interaction.FollowupAsync("Select required response time (optional):", components: components);
    }

    /// <summary>
    ///     Handles final confirmation.
    /// </summary>
    [ComponentInteraction("btn_confirm:*", true)]
    public async Task HandleConfirmation(ulong panelId)
    {
        // Get all stored settings
        var style = await cache.Redis.GetDatabase().StringGetAsync($"btn_creation:{ctx.User.Id}:style");
        var label = await cache.Redis.GetDatabase().StringGetAsync($"btn_creation:{ctx.User.Id}:label");
        var emoji = await cache.Redis.GetDatabase().StringGetAsync($"btn_creation:{ctx.User.Id}:emoji");
        var category = await cache.Redis.GetDatabase().StringGetAsync($"btn_creation:{ctx.User.Id}:category");
        var archiveCategory =
            await cache.Redis.GetDatabase().StringGetAsync($"btn_creation:{ctx.User.Id}:archive_category");
        var roles = await cache.Redis.GetDatabase().StringGetAsync($"btn_creation:{ctx.User.Id}:roles");
        var viewerRoles = await cache.Redis.GetDatabase().StringGetAsync($"btn_creation:{ctx.User.Id}:viewer_roles");
        var openMessage = await cache.Redis.GetDatabase().StringGetAsync($"btn_creation:{ctx.User.Id}:open_message");
        var modalJson = await cache.Redis.GetDatabase().StringGetAsync($"btn_creation:{ctx.User.Id}:modal_json");
        var priorities = await cache.Redis.GetDatabase().StringGetAsync($"btn_creation:{ctx.User.Id}:priorities");
        var defaultPriority =
            await cache.Redis.GetDatabase().StringGetAsync($"btn_creation:{ctx.User.Id}:default_priority");
        var autoClose = await cache.Redis.GetDatabase().StringGetAsync($"btn_creation:{ctx.User.Id}:autoclose");

        // Create the button
        var panel = await Service.GetPanelAsync(panelId);
        if (panel == null)
        {
            await RespondAsync("Panel not found!", ephemeral: true);
            return;
        }

        try
        {
            // Parse style
            var buttonStyle = ButtonStyle.Primary; // default
            if (style.HasValue)
            {
                if (Enum.TryParse<ButtonStyle>(style.ToString(), true, out var parsedStyle))
                {
                    buttonStyle = parsedStyle;
                }
            }

            // Parse category IDs
            ulong? categoryId = null;
            if (category.HasValue)
            {
                if (ulong.TryParse(category, out var parsedCategory))
                {
                    categoryId = parsedCategory;
                }
                else
                {
                    await RespondAsync("Invalid category ID provided.", ephemeral: true);
                    return;
                }
            }

            ulong? archiveCategoryId = null;
            if (archiveCategory.HasValue)
            {
                if (ulong.TryParse(archiveCategory, out var parsedArchiveCategory))
                {
                    archiveCategoryId = parsedArchiveCategory;
                }
                else
                {
                    await RespondAsync("Invalid archive category ID provided.", ephemeral: true);
                    return;
                }
            }

            // Parse roles
            List<ulong> supportRoles = null;
            if (roles.HasValue)
            {
                try
                {
                    var rolesArray = JsonSerializer.Deserialize<string[]>(roles);
                    supportRoles = rolesArray.Select(ulong.Parse).ToList();
                }
                catch
                {
                    await RespondAsync("Invalid roles data.", ephemeral: true);
                    return;
                }
            }

            // Parse viewer roles
            List<ulong> viewerRolesList = null;
            if (viewerRoles.HasValue)
            {
                try
                {
                    viewerRolesList = JsonSerializer.Deserialize<string[]>(viewerRoles).Select(ulong.Parse).ToList();
                }
                catch
                {
                    await RespondAsync("Invalid viewer roles data.", ephemeral: true);
                    return;
                }
            }

            // Parse priorities
            List<string> allowedPriorities = null;
            if (priorities.HasValue)
            {
                try
                {
                    allowedPriorities = JsonSerializer.Deserialize<List<string>>(priorities);
                }
                catch
                {
                    await RespondAsync("Invalid priorities data.", ephemeral: true);
                    return;
                }
            }

            // Parse default priority
            string defaultPriorityValue = null;
            if (defaultPriority.HasValue)
            {
                defaultPriorityValue = defaultPriority;
            }

            // Parse autoCloseTime
            TimeSpan? autoCloseTime = null;
            if (autoClose.HasValue)
            {
                if (int.TryParse(autoClose, out var autoCloseHours))
                {
                    autoCloseTime = TimeSpan.FromHours(autoCloseHours);
                }
                else
                {
                    await RespondAsync("Invalid auto-close time provided.", ephemeral: true);
                    return;
                }
            }

            var button = await Service.AddButtonAsync(
                panel,
                label.HasValue ? label.ToString() : null,
                emoji.HasValue ? emoji.ToString() : null,
                buttonStyle,
                categoryId: categoryId,
                archiveCategoryId: archiveCategoryId,
                supportRoles: supportRoles,
                viewerRoles: viewerRolesList,
                openMessageJson: openMessage.HasValue ? openMessage.ToString() : null,
                modalJson: modalJson.HasValue ? modalJson.ToString() : null,
                allowedPriorities: allowedPriorities,
                defaultPriority: defaultPriorityValue,
                autoCloseTime: autoCloseTime
            );

            await RespondAsync("Button created successfully!", ephemeral: true);
        }
        catch (Exception ex)
        {
            await RespondAsync($"Error creating button: {ex.Message}", ephemeral: true);
        }
        finally
        {
            // Cleanup
            await cache.Redis.GetDatabase().KeyDeleteAsync(new RedisKey[]
            {
                $"btn_creation:{ctx.User.Id}:style", $"btn_creation:{ctx.User.Id}:label",
                $"btn_creation:{ctx.User.Id}:emoji", $"btn_creation:{ctx.User.Id}:category",
                $"btn_creation:{ctx.User.Id}:archive_category", $"btn_creation:{ctx.User.Id}:roles",
                $"btn_creation:{ctx.User.Id}:viewer_roles", $"btn_creation:{ctx.User.Id}:open_message",
                $"btn_creation:{ctx.User.Id}:modal_json", $"btn_creation:{ctx.User.Id}:priorities",
                $"btn_creation:{ctx.User.Id}:default_priority", $"btn_creation:{ctx.User.Id}:autoclose"
            });
        }
    }


    /// <summary>
    ///     Handles auto-close time selection.
    /// </summary>
    [ComponentInteraction("btn_autoclose:*", true)]
    public async Task HandleAutoCloseSelect(string panelId, string[] values)
    {
        try
        {
            // If skip button wasn't clicked and we have a value
            if (values.Any())
            {
                await cache.Redis.GetDatabase()
                    .StringSetAsync($"btn_creation:{ctx.User.Id}:autoclose", values[0]);
            }

            // Show final confirmation
            var components = new ComponentBuilder()
                .WithButton("Confirm", $"btn_confirm:{panelId}")
                .WithButton("Cancel", $"btn_cancel:{panelId}", ButtonStyle.Danger)
                .Build();

            await RespondAsync("Review and confirm button creation:", components: components);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error handling auto-close selection");
            await RespondAsync("An error occurred while processing your selection.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Handles auto-close skip button.
    /// </summary>
    [ComponentInteraction("btn_autoclose:*:skip", true)]
    public async Task HandleAutoCloseSkip(string panelId)
    {
        try
        {
            // Show final confirmation without setting auto-close
            var components = new ComponentBuilder()
                .WithButton("Confirm", $"btn_confirm:{panelId}")
                .WithButton("Cancel", $"btn_cancel:{panelId}", ButtonStyle.Danger)
                .Build();

            await RespondAsync("Review and confirm button creation:", components: components);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error handling auto-close skip");
            await RespondAsync("An error occurred while processing your request.", ephemeral: true);
        }
    }

    #endregion
}
using System.Text.Json;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Modals;
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
    /// Deletes a ticket panel.
    /// </summary>
    /// <param name="panelId">The ID of the panel to delete.</param>
    [SlashCommand("deletepanel", "Deletes a ticket panel")]
    [SlashUserPerm(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    public async Task DeletePanel(
        [Summary("panel-id", "ID of the panel to delete")]
        int panelId)
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
    /// Removes a staff member's claim from a ticket.
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
    ///     Claims a ticket for handling.
    /// </summary>
    /// <param name="ticket">The ID of the ticket to claim.</param>
    [SlashCommand("claim", "Claims a ticket")]
    [RequireContext(ContextType.Guild)]
    public async Task ClaimTicket(
        [Summary("ticket", "The ticket to claim")]
        int ticket
    )
    {
        var ticketObj = await Service.GetTicketAsync(ticket);
        if (ticketObj == null)
        {
            await RespondAsync("Ticket not found!", ephemeral: true);
            return;
        }

        await Service.ClaimTicketAsync(ticketObj, ctx.User as IGuildUser);
        await RespondAsync("Ticket claimed successfully!", ephemeral: true);
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
        var panel = await Service.CreatePanelAsync(ctx.Guild, channel, modal.EmbedJson);
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
            await ctx.Interaction.SendConfirmAsync("Please enter the label for the button:");
            var label = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);

            if (string.IsNullOrEmpty(label))
            {
                await ctx.Interaction.SendErrorAsync("Button creation canceled - no label provided.", Config);
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
            await ctx.Interaction.SendConfirmAsync("Please enter the emoji you'd like to use:");
            var emoji = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);

            if (!string.IsNullOrEmpty(emoji))
            {
                await cache.Redis.GetDatabase().StringSetAsync($"btn_creation:{ctx.User.Id}:emoji", emoji);
            }
        }

        await PromptTicketSettings(panelId);
    }

    /// <summary>
    ///     Handles ticket button clicks.
    /// </summary>
    /// <param name="button">The button's custom ID.</param>
    [ComponentInteraction("ticket_btn_*", true)]
    public async Task HandleTicketButton(string button)
    {
        var panelButton = await Service.GetButtonAsync($"ticket_btn_{button}");
        if (panelButton == null) return;

        try
        {
            if (!string.IsNullOrEmpty(panelButton.ModalJson))
            {
                var modalData = JsonSerializer.Deserialize<Dictionary<string, string>>(panelButton.ModalJson);
                var modal = new ModalBuilder()
                    .WithTitle("Create Ticket")
                    .WithCustomId($"ticket_modal_{panelButton.Id}");

                foreach (var field in modalData)
                {
                    modal.AddTextInput(field.Key, field.Value, required: true);
                }

                await RespondWithModalAsync(modal.Build());
            }
            else
            {
                await Service.CreateTicketAsync(
                    ctx.Guild,
                    ctx.User,
                    panelButton);

                await RespondAsync("Ticket created!", ephemeral: true);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating ticket from button");
            await RespondAsync("Failed to create ticket. Please try again later.", ephemeral: true);
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

        await ctx.Interaction.SendConfirmAsync("Please enter the ticket opening message (or type 'skip' to skip):");

        var openMsg = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
        if (!string.IsNullOrEmpty(openMsg) && openMsg.ToLower() != "skip")
        {
            await cache.Redis.GetDatabase()
                .StringSetAsync($"btn_creation:{ctx.User.Id}:open_message", openMsg);
        }

        await ctx.Interaction.SendConfirmAsync("Please enter the modal JSON configuration (or type 'skip' to skip):");

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
            await ctx.Interaction.SendConfirmAsync("Please enter the default priority (or type 'skip' to skip):");
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
    public async Task HandleConfirmation(string panelId)
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
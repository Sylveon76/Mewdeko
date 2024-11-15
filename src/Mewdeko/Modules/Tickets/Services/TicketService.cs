using System.Text.Json;
using Mewdeko.Database.DbContextStuff;
using Microsoft.EntityFrameworkCore;
using Serilog;
using SelectMenuOption = Mewdeko.Database.Models.SelectMenuOption;

namespace Mewdeko.Services;

/// <summary>
///     Service for managing ticket panels, tickets, and cases.
/// </summary>
public class TicketService : INService
{
    private readonly DbContextProvider _db;
    private readonly DiscordShardedClient _client;
    private readonly IDataCache _cache;
    private const string ClaimButtonId = "ticket_claim";
    private const string CloseButtonId = "ticket_close";

    /// <summary>
    ///     Initializes a new instance of the <see cref="TicketService" /> class.
    /// </summary>
    public TicketService(
        DbContextProvider db,
        DiscordShardedClient client,
        GuildSettingsService guildSettings,
        IDataCache cache,
        EventHandler eventHandler)
    {
        _db = db;
        _client = client;
        _cache = cache;

        eventHandler.MessageDeleted += HandleMessageDeleted;
    }

    /// <summary>
    ///     Creates a new ticket panel.
    /// </summary>
    /// <param name="guild">The guild where the panel will be created.</param>
    /// <param name="channel">The channel where the panel will be displayed.</param>
    /// <param name="embedJson">The JSON configuration for the panel's embed.</param>
    public async Task<TicketPanel?> CreatePanelAsync(IGuild guild, ITextChannel channel, string embedJson)
    {
        try
        {
            await using var ctx = await _db.GetContextAsync();

            SmartEmbed.TryParse(embedJson, guild.Id, out var embeds, out var plainText, out _);

            var message = await channel.SendMessageAsync(plainText, embeds: embeds);

            var panel = new TicketPanel
            {
                GuildId = guild.Id, ChannelId = channel.Id, MessageId = message.Id, EmbedJson = embedJson
            };

            ctx.TicketPanels.Add(panel);
            await ctx.SaveChangesAsync();

            return panel;
        }
        catch (Exception e)
        {
            Log.Error(e, "OOPS");
        }

        return null;
    }

    /// <summary>
    ///     Adds a button to an existing ticket panel.
    /// </summary>
    /// <param name="panel">The panel to add the button to.</param>
    /// <param name="label">The button label.</param>
    /// <param name="emoji">Optional emoji for the button.</param>
    /// <param name="style">The button style.</param>
    /// <param name="openMessageJson">Optional JSON for ticket opening message.</param>
    /// <param name="modalJson">Optional JSON for ticket creation modal.</param>
    /// <param name="channelFormat">Format for ticket channel names.</param>
    /// <param name="categoryId">Optional category for ticket channels.</param>
    /// <param name="archiveCategoryId">Optional category for archived tickets.</param>
    /// <param name="supportRoles">List of support role IDs.</param>
    /// <param name="viewerRoles">List of viewer role IDs.</param>
    /// <param name="autoCloseTime">Optional auto-close duration.</param>
    /// <param name="requiredResponseTime">Optional required response time.</param>
    /// <param name="maxActiveTickets">Maximum active tickets per user.</param>
    /// <param name="allowedPriorities">List of allowed priority IDs.</param>
    /// <param name="defaultPriority">Optional default priority.</param>
    public async Task<PanelButton> AddButtonAsync(
        TicketPanel panel,
        string label,
        string emoji = null,
        ButtonStyle style = ButtonStyle.Primary,
        string openMessageJson = null,
        string modalJson = null,
        string channelFormat = "ticket-{username}-{id}",
        ulong? categoryId = null,
        ulong? archiveCategoryId = null,
        List<ulong> supportRoles = null,
        List<ulong> viewerRoles = null,
        TimeSpan? autoCloseTime = null,
        TimeSpan? requiredResponseTime = null,
        int maxActiveTickets = 1,
        List<string> allowedPriorities = null,
        string defaultPriority = null)
    {
        await using var ctx = await _db.GetContextAsync();

        var button = new PanelButton
        {
            PanelId = panel.Id,
            Label = label,
            Emoji = emoji,
            CustomId = $"ticket_btn_{Guid.NewGuid():N}",
            Style = style,
            OpenMessageJson = openMessageJson,
            ModalJson = modalJson,
            ChannelNameFormat = channelFormat,
            CategoryId = categoryId,
            ArchiveCategoryId = archiveCategoryId,
            SupportRoles = supportRoles ?? new List<ulong>(),
            ViewerRoles = viewerRoles ?? new List<ulong>(),
            AutoCloseTime = autoCloseTime,
            RequiredResponseTime = requiredResponseTime,
            MaxActiveTickets = maxActiveTickets,
            AllowedPriorities = allowedPriorities ?? new List<string>(),
            DefaultPriority = defaultPriority
        };

        ctx.Attach(panel);
        panel.Buttons.Add(button);
        await ctx.SaveChangesAsync();
        await UpdatePanelComponentsAsync(panel);

        return button;
    }

    /// <summary>
    ///     Updates a button's properties.
    /// </summary>
    public async Task UpdateButtonAsync(PanelButton button, Action<PanelButton> updateAction)
    {
        await using var ctx = await _db.GetContextAsync();

        ctx.Attach(button);
        updateAction(button);

        await UpdatePanelComponentsAsync(button.Panel);
        await ctx.SaveChangesAsync();
    }

    private MessageComponent GetDefaultTicketComponents()
    {
        var claimButton = new ButtonBuilder()
            .WithCustomId(ClaimButtonId)
            .WithLabel("Claim")
            .WithStyle(ButtonStyle.Primary)
            .WithEmote(new Emoji("🤝"));

        var closeButton = new ButtonBuilder()
            .WithCustomId(CloseButtonId)
            .WithLabel("Close")
            .WithStyle(ButtonStyle.Danger)
            .WithEmote(new Emoji("🔒"));

        return new ComponentBuilder()
            .WithButton(claimButton)
            .WithButton(closeButton)
            .Build();
    }

    /// <summary>
    ///     Retrieves a case by its ID.
    /// </summary>
    /// <param name="caseId">The ID of the case to retrieve.</param>
    /// <returns>A task containing the case if found, null otherwise.</returns>
    public async Task<TicketCase> GetCaseAsync(int caseId)
    {
        await using var ctx = await _db.GetContextAsync();
        return await ctx.TicketCases
            .Include(c => c.LinkedTickets)
            .Include(c => c.Notes)
            .FirstOrDefaultAsync(c => c.Id == caseId);
    }

    /// <summary>
    ///     Closes a case and optionally archives linked tickets.
    /// </summary>
    /// <param name="ticketCase">The case to close.</param>
    /// <param name="archiveTickets">Whether to archive linked tickets. Defaults to false.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task CloseCaseAsync(TicketCase ticketCase, bool archiveTickets = false)
    {
        await using var ctx = await _db.GetContextAsync();
        ctx.Attach(ticketCase);
        ticketCase.ClosedAt = DateTime.UtcNow;

        if (archiveTickets && ticketCase.LinkedTickets.Any())
        {
            foreach (var ticket in ticketCase.LinkedTickets)
            {
                if (!ticket.IsArchived && !ticket.ClosedAt.HasValue)
                {
                    ticket.ClosedAt = DateTime.UtcNow;
                    ticket.IsArchived = true;

                    // If the ticket has an archive category set, move it
                    var button = ticket.Button;
                    var option = ticket.SelectOption;
                    var archiveCategoryId = button?.ArchiveCategoryId ?? option?.ArchiveCategoryId;

                    if (archiveCategoryId.HasValue)
                    {
                        var guild = await _client.Rest.GetGuildAsync(ticket.GuildId);
                        if (guild != null)
                        {
                            var channel = await guild.GetTextChannelAsync(ticket.ChannelId);
                            if (channel != null)
                            {
                                await channel.ModifyAsync(props =>
                                    props.CategoryId = archiveCategoryId.Value);
                            }
                        }
                    }
                }
            }
        }

        await ctx.SaveChangesAsync();
    }

    /// <summary>
    ///     Retrieves all cases for a guild, ordered by creation date.
    /// </summary>
    /// <param name="guildId">The ID of the guild to get cases for.</param>
    /// <param name="includeDeleted">Whether to include soft-deleted cases. Defaults to false.</param>
    /// <returns>A task containing the list of cases.</returns>
    public async Task<List<TicketCase>> GetGuildCasesAsync(ulong guildId, bool includeDeleted = false)
    {
        await using var ctx = await _db.GetContextAsync();
        var query = ctx.TicketCases
            .Include(c => c.LinkedTickets)
            .Include(c => c.Notes)
            .Where(c => c.GuildId == guildId);

        if (!includeDeleted)
            query = query.Where(c => !c.ClosedAt.HasValue);

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    ///     Adds a note to a case.
    /// </summary>
    /// <param name="caseId">The ID of the case to add the note to.</param>
    /// <param name="authorId">The ID of the user adding the note.</param>
    /// <param name="content">The content of the note.</param>
    /// <returns>A task containing the created note.</returns>
    public async Task<CaseNote?> AddCaseNoteAsync(int caseId, ulong authorId, string content)
    {
        await using var ctx = await _db.GetContextAsync();
        var ticketCase = await ctx.TicketCases.FindAsync(caseId);
        if (ticketCase == null)
            return null;

        var note = new CaseNote
        {
            CaseId = caseId, AuthorId = authorId, Content = content, CreatedAt = DateTime.UtcNow
        };

        ctx.CaseNotes.Add(note);
        await ctx.SaveChangesAsync();
        return note;
    }

    /// <summary>
/// Deletes a ticket panel and all its associated components.
/// </summary>
/// <param name="panelId">The ID of the panel to delete.</param>
/// <param name="guild">The guild containing the panel.</param>
/// <returns>A task that represents the asynchronous operation.</returns>
/// <exception cref="InvalidOperationException">Thrown when the panel is not found.</exception>
public async Task DeletePanelAsync(int panelId, IGuild guild)
{
    await using var ctx = await _db.GetContextAsync();
    await using var transaction = await ctx.Database.BeginTransactionAsync();

    try
    {
        // First get the panel with its buttons to handle dependencies
        var panel = await ctx.TicketPanels
            .Include(p => p.Buttons)
            .FirstOrDefaultAsync(p => p.Id == panelId);

        if (panel == null)
            throw new InvalidOperationException("Panel not found");

        // Try to delete the Discord message if it exists
        try
        {
            var channel = await guild.GetChannelAsync(panel.ChannelId) as ITextChannel;
            if (channel != null)
            {
                var message = await channel.GetMessageAsync(panel.MessageId);
                if (message != null)
                    await message.DeleteAsync();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to delete panel message");
            // Continue with database cleanup
        }

        // Handle tickets that reference the panel's buttons
        if (panel.Buttons?.Any() == true)
        {
            var buttonIds = panel.Buttons.Select(b => b.Id).ToList();

            // Find all tickets referencing these buttons
            var tickets = await ctx.Tickets
                .Where(t => t.ButtonId.HasValue && buttonIds.Contains(t.ButtonId.Value))
                .ToListAsync();

            // Update tickets to remove button references
            foreach (var ticket in tickets)
            {
                ticket.ButtonId = null;
                ticket.Button = null;
            }
            await ctx.SaveChangesAsync();
        }

        // Handle select menu options that may be referenced by tickets
        try
        {
            var menus = await ctx.PanelSelectMenus
                .Include(m => m.Options)
                .Where(m => m.PanelId == panelId)
                .ToListAsync();

            if (menus.Any())
            {
                var optionIds = menus.SelectMany(m => m.Options).Select(o => o.Id).ToList();

                // Update tickets to remove select option references
                var tickets = await ctx.Tickets
                    .Where(t => t.SelectOptionId.HasValue && optionIds.Contains(t.SelectOptionId.Value))
                    .ToListAsync();

                foreach (var ticket in tickets)
                {
                    ticket.SelectOptionId = null;
                    ticket.SelectOption = null;
                }
                await ctx.SaveChangesAsync();

                // Now safe to remove menus and options
                ctx.PanelSelectMenus.RemoveRange(menus);
                await ctx.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to handle select menus - table may not exist");
            // Continue with deletion
        }

        // Finally remove the panel itself
        ctx.TicketPanels.Remove(panel);
        await ctx.SaveChangesAsync();

        await transaction.CommitAsync();
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        Log.Error(ex, "Failed to delete panel {PanelId}", panelId);
        throw new InvalidOperationException($"Failed to delete panel: {ex.Message}", ex);
    }
}

    /// <summary>
    ///     Removes a staff member's claim from a ticket.
    /// </summary>
    /// <param name="ticket">The ticket to unclaim.</param>
    /// <param name="moderator">The moderator performing the unclaim action.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the ticket is not claimed.</exception>
    public async Task UnclaimTicketAsync(Ticket ticket, IGuildUser moderator)
    {
        if (!ticket.ClaimedBy.HasValue)
            throw new InvalidOperationException("Ticket is not claimed");

        await using var ctx = await _db.GetContextAsync();
        ctx.Attach(ticket);

        var previousClaimer = ticket.ClaimedBy.Value;
        ticket.ClaimedBy = null;
        ticket.LastActivityAt = DateTime.UtcNow;

        if (await moderator.Guild.GetChannelAsync(ticket.ChannelId) is ITextChannel channel)
        {
            var embed = new EmbedBuilder()
                .WithTitle("Ticket Unclaimed")
                .WithDescription($"This ticket has been unclaimed by {moderator.Mention}")
                .WithColor(Color.Orange)
                .Build();

            await channel.SendMessageAsync(embed: embed);

            // Notify previous claimer if enabled
            var settings = await ctx.GuildTicketSettings.FirstOrDefaultAsync(s => s.GuildId == moderator.Guild.Id);
            if (settings?.EnableDmNotifications == true)
            {
                try
                {
                    var previousUser = await moderator.Guild.GetUserAsync(previousClaimer);
                    if (previousUser != null)
                    {
                        var dmEmbed = new EmbedBuilder()
                            .WithTitle("Ticket Unclaimed")
                            .WithDescription($"Your claim on ticket #{ticket.Id} has been removed by {moderator}")
                            .WithColor(Color.Orange)
                            .Build();

                        await previousUser.SendMessageAsync(embed: dmEmbed);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to send DM notification for ticket unclaim");
                }
            }
        }

        await ctx.SaveChangesAsync();
    }

    /// <summary>
    ///     Gets or creates the default ticket opening message.
    /// </summary>
    /// <param name="ticket">The ticket being opened.</param>
    /// <param name="customMessage">Optional custom message to override the default.</param>
    /// <returns>The configured message content in SmartEmbed format.</returns>
    private string GetTicketOpenMessage(Ticket ticket, string customMessage = null)
    {
        if (!string.IsNullOrWhiteSpace(customMessage))
            return customMessage;

        // Build default embed
        var embed = new
        {
            title = "New Ticket Created",
            description = $"Ticket ID: {ticket.Id}\nCreated by: <@{ticket.CreatorId}>",
            color = "info",
            fields = new[]
            {
                new
                {
                    name = "Instructions",
                    value = "Please describe your issue and wait for a staff member to assist you."
                }
            },
            footer = new
            {
                text = $"Created at {DateTime.UtcNow:g} UTC"
            }
        };

        return JsonSerializer.Serialize(new[]
        {
            embed
        });
    }

    private async Task SendDefaultOpenMessage(ITextChannel channel, Ticket ticket)
    {
        var defaultMessage = GetTicketOpenMessage(ticket);
        SmartEmbed.TryParse(defaultMessage, channel.GuildId, out var embeds, out var plainText, out _);
        await channel.SendMessageAsync(plainText, embeds: embeds, components: GetDefaultTicketComponents());
    }

    /// <summary>
    ///     Edits an existing case note.
    /// </summary>
    /// <param name="noteId">The ID of the note to edit.</param>
    /// <param name="editorId">The ID of the user editing the note.</param>
    /// <param name="newContent">The new content for the note.</param>
    /// <returns>A task containing true if the edit was successful, false otherwise.</returns>
    public async Task<bool> EditCaseNoteAsync(int noteId, ulong editorId, string newContent)
    {
        await using var ctx = await _db.GetContextAsync();
        var note = await ctx.CaseNotes.FindAsync(noteId);
        if (note == null)
            return false;

        var oldContent = note.Content;
        note.Content = newContent;

        var edit = new NoteEdit
        {
            OldContent = oldContent, NewContent = newContent, EditorId = editorId, EditedAt = DateTime.UtcNow
        };

        note.EditHistory.Add(edit);
        await ctx.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Deletes a case note.
    /// </summary>
    /// <param name="noteId">The ID of the note to delete.</param>
    /// <returns>A task containing true if the deletion was successful, false otherwise.</returns>
    public async Task<bool> DeleteCaseNoteAsync(int noteId)
    {
        await using var ctx = await _db.GetContextAsync();
        var note = await ctx.CaseNotes.FindAsync(noteId);
        if (note == null)
            return false;

        ctx.CaseNotes.Remove(note);
        await ctx.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Unlinks a collection of tickets from their associated cases.
    /// </summary>
    /// <param name="tickets">The collection of tickets to unlink.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task UnlinkTicketsFromCase(IEnumerable<Ticket> tickets)
    {
        await using var ctx = await _db.GetContextAsync();
        foreach (var ticket in tickets)
        {
            ctx.Attach(ticket);
            ticket.CaseId = null;
            ticket.Case = null;
        }

        await ctx.SaveChangesAsync();
    }

    /// <summary>
    ///     Reopens a previously closed case.
    /// </summary>
    /// <param name="ticketCase">The case to reopen.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ReopenCaseAsync(TicketCase ticketCase)
    {
        await using var ctx = await _db.GetContextAsync();
        ctx.Attach(ticketCase);
        ticketCase.ClosedAt = null;
        await ctx.SaveChangesAsync();
    }

    /// <summary>
    ///     Updates the details of an existing case.
    /// </summary>
    /// <param name="caseId">The ID of the case to update.</param>
    /// <param name="title">The new title for the case. If null, the title remains unchanged.</param>
    /// <param name="description">The new description for the case. If null, the description remains unchanged.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task UpdateCaseAsync(int caseId, string title, string description)
    {
        await using var ctx = await _db.GetContextAsync();
        var ticketCase = await ctx.TicketCases.FindAsync(caseId);
        if (ticketCase != null)
        {
            if (!string.IsNullOrEmpty(title))
                ticketCase.Title = title;
            if (!string.IsNullOrEmpty(description))
                ticketCase.Description = description;
            await ctx.SaveChangesAsync();
        }
    }

    //// <summary>
    /// Creates a new ticket.
    /// </summary>
    /// <param name="guild">The guild where the ticket will be created.</param>
    /// <param name="creator">The user creating the ticket.</param>
    /// <param name="button">Optional button that triggered the ticket creation.</param>
    /// <param name="option">Optional select menu option that triggered the ticket creation.</param>
    /// <param name="modalResponses">Optional responses from a modal form.</param>
    /// <returns>The created ticket.</returns>
    /// <exception cref="InvalidOperationException">Thrown when ticket creation fails due to limits or permissions.</exception>
    public async Task<Ticket> CreateTicketAsync(
        IGuild guild,
        IUser creator,
        PanelButton button = null,
        SelectMenuOption option = null,
        Dictionary<string, string> modalResponses = null)
    {
        await using var ctx = await _db.GetContextAsync();

        // Check if user is blacklisted
        var settings = await ctx.GuildTicketSettings.FirstOrDefaultAsync(s => s.GuildId == guild.Id);
        if (settings?.BlacklistedUsers?.Contains(creator.Id) == true)
        {
            throw new InvalidOperationException("You are blacklisted from creating tickets.");
        }

        var id = button?.Id ?? option.Id;

        // Check if user is blacklisted from this specific ticket type
        if (settings?.BlacklistedTypes?.TryGetValue(creator.Id, out var blacklistedTypes) == true)
        {
            if (blacklistedTypes.Contains(id.ToString()))
            {
                throw new InvalidOperationException("You are blacklisted from creating this type of ticket.");
            }
        }

        // Validate ticket limits
        var maxTickets = button?.MaxActiveTickets ?? option?.MaxActiveTickets ?? settings?.DefaultMaxTickets ?? 1;
        var activeTickets = await GetActiveTicketsAsync(guild.Id, creator.Id, id);

        if (activeTickets.Count >= maxTickets)
        {
            throw new InvalidOperationException($"You can only have {maxTickets} active tickets of this type.");
        }

        // Create ticket channel
        var categoryId = button?.CategoryId ?? option?.CategoryId;
        var category = categoryId.HasValue ? await guild.GetCategoryChannelAsync(categoryId.Value) : null;

        var channelName = (button?.ChannelNameFormat ?? option?.ChannelNameFormat ?? "ticket-{username}-{id}")
            .Replace("{username}", creator.Username.ToLower())
            .Replace("{id}", (activeTickets.Count + 1).ToString());

        ITextChannel channel;
        try
        {
            channel = await guild.CreateTextChannelAsync(channelName, props =>
            {
                if (category != null)
                    props.CategoryId = category.Id;
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create ticket channel");
            throw new InvalidOperationException("Failed to create ticket channel. Please check bot permissions.");
        }

        // Set permissions
        await SetTicketPermissionsAsync(channel, creator, button, option);

        // Create ticket entity
        var ticket = new Ticket
        {
            GuildId = guild.Id,
            ChannelId = channel.Id,
            CreatorId = creator.Id,
            ButtonId = button?.Id,
            SelectOptionId = option?.Id,
            ModalResponses = modalResponses != null ? JsonSerializer.Serialize(modalResponses) : null,
            Priority = button?.DefaultPriority ?? option?.DefaultPriority,
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow
        };

        ctx.Tickets.Add(ticket);
        await ctx.SaveChangesAsync();

        // Send messages in order
        try
        {
            // 1. Opening message (custom or default)
            var openMessageJson = button?.OpenMessageJson ?? option?.OpenMessageJson;
            if (!string.IsNullOrEmpty(openMessageJson))
            {
                SmartEmbed.TryParse(openMessageJson, guild.Id, out var embeds, out var plainText, out _);
                await channel.SendMessageAsync(plainText, embeds: embeds, components: GetDefaultTicketComponents());
            }
            else
            {
                await SendDefaultOpenMessage(channel, ticket);
            }

            // 2. Modal responses if any
            if (modalResponses?.Any() == true)
            {
                var modalEmbed = new EmbedBuilder()
                    .WithTitle("Ticket Information")
                    .WithDescription(string.Join("\n", modalResponses.Select(r => $"**{r.Key}**: {r.Value}")))
                    .WithColor(Color.Blue)
                    .Build();

                await channel.SendMessageAsync(embed: modalEmbed);
            }

            // Send notifications
            await SendTicketNotificationsAsync(ticket, creator, guild, settings);

            // Log ticket creation
            if (settings?.LogChannelId.HasValue == true)
            {
                var logChannel = await guild.GetTextChannelAsync(settings.LogChannelId.Value);
                if (logChannel != null)
                {
                    var logEmbed = new EmbedBuilder()
                        .WithTitle("New Ticket Created")
                        .WithDescription($"Ticket #{ticket.Id} created by {creator.Mention}")
                        .AddField("Channel", channel.Mention, true)
                        .AddField("Type", button != null ? $"Button: {button.Label}" : $"Option: {option.Label}", true)
                        .WithColor(Color.Green)
                        .WithCurrentTimestamp()
                        .Build();

                    await logChannel.SendMessageAsync(embed: logEmbed);
                }
            }

            return ticket;
        }
        catch (Exception ex)
        {
            // Cleanup on failure
            Log.Error(ex, "Error during ticket creation messages/notifications");
            try
            {
                await channel.DeleteAsync();
                ctx.Tickets.Remove(ticket);
                await ctx.SaveChangesAsync();
            }
            catch (Exception cleanupEx)
            {
                Log.Error(cleanupEx, "Error during ticket cleanup");
            }

            throw new InvalidOperationException("Failed to complete ticket creation.");
        }
    }

    /// <summary>
    ///     Sends notifications about a new ticket to relevant staff members.
    /// </summary>
    private async Task SendTicketNotificationsAsync(Ticket ticket, IUser creator, IGuild guild,
        GuildTicketSettings settings)
    {
        try
        {
            var channel = await guild.GetTextChannelAsync(ticket.ChannelId);
            if (channel == null) return;

            var supportRoles = ticket.Button?.SupportRoles ?? ticket.SelectOption?.SupportRoles ?? new List<ulong>();
            var notificationRoles = settings?.NotificationRoles ?? new List<ulong>();
            var allRoles = supportRoles.Concat(notificationRoles).Distinct();

            if (settings?.EnableStaffPings == true)
            {
                var mentions = string.Join(" ", allRoles.Select(r => $"<@&{r}>"));
                if (!string.IsNullOrEmpty(mentions))
                {
                    await channel.SendMessageAsync($"{mentions} A new ticket requires attention.");
                }
            }

            if (settings?.EnableDmNotifications == true)
            {
                foreach (var roleId in allRoles)
                {
                    var role = guild.GetRole(roleId);
                    if (role == null) continue;

                    foreach (var member in await role.GetMembersAsync())
                    {
                        try
                        {
                            if (member.IsBot) continue;

                            var dmEmbed = new EmbedBuilder()
                                .WithTitle("New Ticket Notification")
                                .WithDescription($"A new ticket has been created in {guild.Name}")
                                .AddField("Creator", creator.ToString(), true)
                                .AddField("Channel", $"#{channel.Name}", true)
                                .WithColor(Color.Blue)
                                .Build();

                            await member.SendMessageAsync(embed: dmEmbed);
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Failed to send DM notification to {UserId}", member.Id);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error sending ticket notifications");
        }
    }

    /// <summary>
    ///     Claims a ticket for a staff member.
    /// </summary>
    public async Task ClaimTicketAsync(Ticket ticket, IGuildUser staff)
    {
        if (ticket.ClaimedBy.HasValue)
            throw new InvalidOperationException("Ticket is already claimed");

        await using var ctx = await _db.GetContextAsync();

        ctx.Attach(ticket);
        ticket.ClaimedBy = staff.Id;
        ticket.ClosedAt = DateTime.UtcNow;
        ticket.LastActivityAt = DateTime.UtcNow;

        if (await staff.Guild.GetChannelAsync(ticket.ChannelId) is ITextChannel channel)
        {
            var embed = new EmbedBuilder()
                .WithTitle("Ticket Claimed")
                .WithDescription($"This ticket has been claimed by {staff.Mention}")
                .WithColor(Color.Green)
                .Build();

            await channel.SendMessageAsync(embed: embed);
        }

        await ctx.SaveChangesAsync();
    }

    /// <summary>
    ///     Adds a note to a ticket.
    /// </summary>
    public async Task<TicketNote> AddNoteAsync(Ticket ticket, IGuildUser author, string content)
    {
        await using var ctx = await _db.GetContextAsync();

        var note = new TicketNote
        {
            TicketId = ticket.Id, AuthorId = author.Id, Content = content, CreatedAt = DateTime.UtcNow
        };

        ctx.Attach(ticket);
        ticket.Notes.Add(note);
        ticket.LastActivityAt = DateTime.UtcNow;

        await ctx.SaveChangesAsync();

        return note;
    }

    /// <summary>
    ///     Creates a new case and optionally links tickets to it.
    /// </summary>
    public async Task<TicketCase> CreateCaseAsync(
        IGuild guild,
        string title,
        string description,
        IGuildUser creator,
        IEnumerable<Ticket> ticketsToLink = null)
    {
        await using var ctx = await _db.GetContextAsync();

        var ticketCase = new TicketCase
        {
            GuildId = guild.Id,
            Title = title,
            Description = description,
            CreatedBy = creator.Id,
            CreatedAt = DateTime.UtcNow
        };

        if (ticketsToLink != null)
        {
            foreach (var ticket in ticketsToLink)
            {
                ctx.Attach(ticket);
                ticket.Case = ticketCase;
            }
        }

        ctx.TicketCases.Add(ticketCase);
        await ctx.SaveChangesAsync();

        return ticketCase;
    }

    /// <summary>
    ///     Archives a ticket.
    /// </summary>
    public async Task ArchiveTicketAsync(Ticket ticket)
    {
        await using var ctx = await _db.GetContextAsync();

        ctx.Attach(ticket);

        IGuild guild = _client.GetGuild(ticket.GuildId);

        if (await guild.GetChannelAsync(ticket.ChannelId) is ITextChannel channel)
        {
            if (ticket.Button?.ArchiveCategoryId != null || ticket.SelectOption?.ArchiveCategoryId != null)
            {
                var categoryId = ticket.Button?.ArchiveCategoryId ?? ticket.SelectOption?.ArchiveCategoryId;
                var category = await channel.Guild.GetCategoryChannelAsync(categoryId.Value);

                if (category != null)
                    await channel.ModifyAsync(c => c.CategoryId = category.Id);
            }

            // Generate transcript if enabled
            if (ticket.Button?.SaveTranscript ?? true)
            {
                // Implementation for transcript generation
                ticket.TranscriptUrl = "transcript_url_here"; // Replace with actual transcript generation
            }
        }

        ticket.IsArchived = true;
        ticket.LastActivityAt = DateTime.UtcNow;

        await ctx.SaveChangesAsync();
    }

    private async Task UpdatePanelComponentsAsync(TicketPanel panel)
    {
        IGuild guild = _client.GetGuild(panel.GuildId);
        var channel = await guild.GetChannelAsync(panel.ChannelId) as ITextChannel;
        var message = await channel?.GetMessageAsync(panel.MessageId);

        if (message is not IUserMessage userMessage)
            return;

        var components = new ComponentBuilder();

        // Add buttons
        if (panel.Buttons?.Any() == true)
        {
            var buttonRow = new ActionRowBuilder();
            foreach (var button in panel.Buttons)
            {
                var btnBuilder = new ButtonBuilder()
                    .WithLabel(button.Label)
                    .WithCustomId(button.CustomId)
                    .WithStyle(button.Style);

                if (!string.IsNullOrEmpty(button.Emoji))
                    btnBuilder.WithEmote(Emote.Parse(button.Emoji));

                buttonRow.WithButton(btnBuilder);
            }

            components.AddRow(buttonRow);
        }

        // Add select menus
        if (panel.SelectMenus?.Any() == true)
        {
            foreach (var menu in panel.SelectMenus)
            {
                var selectBuilder = new SelectMenuBuilder()
                    .WithCustomId(menu.CustomId)
                    .WithPlaceholder(menu.Placeholder);

                foreach (var option in menu.Options)
                {
                    var optBuilder = new SelectMenuOptionBuilder()
                        .WithLabel(option.Label)
                        .WithValue(option.Value)
                        .WithDescription(option.Description);

                    if (!string.IsNullOrEmpty(option.Emoji))
                        optBuilder.WithEmote(Emote.Parse(option.Emoji));

                    selectBuilder.AddOption(optBuilder);
                }

                components.AddRow(new ActionRowBuilder().WithSelectMenu(selectBuilder));
            }
        }

        await userMessage.ModifyAsync(m => m.Components = components.Build());
    }

    private async Task SetTicketPermissionsAsync(ITextChannel channel, IUser creator, PanelButton button = null,
        SelectMenuOption option = null)
    {
        var supportRoles = button?.SupportRoles ?? option?.SupportRoles ?? new List<ulong>();
        var viewerRoles = button?.ViewerRoles ?? option?.ViewerRoles ?? new List<ulong>();

        // Deny everyone
        await channel.AddPermissionOverwriteAsync(channel.Guild.EveryoneRole,
            new OverwritePermissions(viewChannel: PermValue.Deny));

        await channel.AddPermissionOverwriteAsync(creator,
            new OverwritePermissions(
                viewChannel: PermValue.Allow,
                sendMessages: PermValue.Allow,
                readMessageHistory: PermValue.Allow,
                attachFiles: PermValue.Allow,
                embedLinks: PermValue.Allow));

        // Support roles get full access
        foreach (var roleId in supportRoles)
        {
            var role = channel.Guild.GetRole(roleId);
            if (role != null)
            {
                await channel.AddPermissionOverwriteAsync(role,
                    new OverwritePermissions(
                        viewChannel: PermValue.Allow,
                        sendMessages: PermValue.Allow,
                        readMessageHistory: PermValue.Allow,
                        attachFiles: PermValue.Allow,
                        embedLinks: PermValue.Allow,
                        manageMessages: PermValue.Allow));
            }
        }

        // Viewer roles can only view
        foreach (var roleId in viewerRoles)
        {
            var role = channel.Guild.GetRole(roleId);
            if (role != null)
            {
                await channel.AddPermissionOverwriteAsync(role,
                    new OverwritePermissions(
                        viewChannel: PermValue.Allow,
                        readMessageHistory: PermValue.Allow,
                        sendMessages: PermValue.Deny));
            }
        }
    }

    private async Task HandleMessageComponent(SocketMessageComponent component)
    {
        await using var ctx = await _db.GetContextAsync();

        // Handle button clicks
        if (component.Data.Type == ComponentType.Button && component.Data.CustomId.StartsWith("ticket_btn_"))
        {
            var button = await ctx.PanelButtons.FirstOrDefaultAsync(b => b.CustomId == component.Data.CustomId);
            if (button == null)
                return;

            try
            {
                if (!string.IsNullOrEmpty(button.ModalJson))
                {
                    // Show modal if configured
                    var modalData = JsonSerializer.Deserialize<Dictionary<string, string>>(button.ModalJson);
                    var modal = new ModalBuilder()
                        .WithTitle("Create Ticket")
                        .WithCustomId($"ticket_modal_{button.Id}");

                    foreach (var field in modalData)
                    {
                        modal.AddTextInput(field.Key, field.Value, required: true);
                    }

                    await component.RespondWithModalAsync(modal.Build());
                }
                else
                {
                    // Create ticket directly
                    await CreateTicketAsync(
                        (component.Channel as IGuildChannel)?.Guild,
                        component.User,
                        button);

                    await component.RespondAsync("Ticket created!", ephemeral: true);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating ticket from button");
                await component.RespondAsync("Failed to create ticket. Please try again later.", ephemeral: true);
            }
        }

        // Handle select menu selections
        if (component.Data.Type == ComponentType.SelectMenu && component.Data.CustomId.StartsWith("ticket_select_"))
        {
            var menuOption = await ctx.SelectMenuOptions
                .Include(o => o.SelectMenu)
                .FirstOrDefaultAsync(o => o.Value == component.Data.Values.First());

            if (menuOption == null)
                return;

            try
            {
                if (!string.IsNullOrEmpty(menuOption.ModalJson))
                {
                    var modalData = JsonSerializer.Deserialize<Dictionary<string, string>>(menuOption.ModalJson);
                    var modal = new ModalBuilder()
                        .WithTitle("Create Ticket")
                        .WithCustomId($"ticket_modal_select_{menuOption.Id}");

                    foreach (var field in modalData)
                    {
                        modal.AddTextInput(field.Key, field.Value, required: true);
                    }

                    await component.RespondWithModalAsync(modal.Build());
                }
                else
                {
                    await CreateTicketAsync(
                        (component.Channel as IGuildChannel)?.Guild,
                        component.User,
                        option: menuOption);

                    await component.RespondAsync("Ticket created!", ephemeral: true);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating ticket from select menu");
                await component.RespondAsync("Failed to create ticket. Please try again later.", ephemeral: true);
            }
        }
    }

    private async Task HandleModalSubmitted(SocketModal modal)
    {
        await using var ctx = await _db.GetContextAsync();

        try
        {
            var responses = modal.Data.Components.ToDictionary(
                x => x.CustomId,
                x => x.Value);

            if (modal.Data.CustomId.StartsWith("ticket_modal_"))
            {
                if (modal.Data.CustomId.Contains("select_"))
                {
                    // Handle select menu modal
                    var optionId = int.Parse(modal.Data.CustomId.Split('_').Last());
                    var option = await ctx.SelectMenuOptions.FindAsync(optionId);

                    if (option != null)
                    {
                        await CreateTicketAsync(
                            (modal.Channel as IGuildChannel)?.Guild,
                            modal.User,
                            option: option,
                            modalResponses: responses);

                        await modal.RespondAsync("Ticket created!", ephemeral: true);
                    }
                }
                else
                {
                    // Handle button modal
                    var buttonId = int.Parse(modal.Data.CustomId.Split('_').Last());
                    var button = await ctx.PanelButtons.FindAsync(buttonId);

                    if (button != null)
                    {
                        await CreateTicketAsync(
                            (modal.Channel as IGuildChannel)?.Guild,
                            modal.User,
                            button,
                            modalResponses: responses);

                        await modal.RespondAsync("Ticket created!", ephemeral: true);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling modal submission");
            await modal.RespondAsync("Failed to create ticket. Please try again later.", ephemeral: true);
        }
    }

    private async Task HandleMessageDeleted(Cacheable<IMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel)
    {
        if (!message.HasValue || !channel.HasValue)
            return;

        await using var ctx = await _db.GetContextAsync();

        // Check if deleted message was a panel
        var panel = await ctx.TicketPanels
            .FirstOrDefaultAsync(p => p.MessageId == message.Value.Id);

        if (panel != null)
        {
            // Panel was deleted, clean up
            ctx.TicketPanels.Remove(panel);
            await ctx.SaveChangesAsync();
        }
    }

    /// <summary>
    ///     Checks for tickets that should be auto-closed and handles them.
    /// </summary>
    public async Task CheckAutoCloseTicketsAsync()
    {
        await using var ctx = await _db.GetContextAsync();

        var tickets = await ctx.Tickets
            .Include(t => t.Button)
            .Include(t => t.SelectOption)
            .Where(t => !t.IsArchived && !t.ClosedAt.HasValue)
            .ToListAsync();

        foreach (var ticket in tickets)
        {
            IGuild guild = _client.GetGuild(ticket.GuildId);
            var autoCloseTime = ticket.Button?.AutoCloseTime ?? ticket.SelectOption?.AutoCloseTime;
            if (!autoCloseTime.HasValue || !ticket.LastActivityAt.HasValue)
                continue;

            if (DateTime.UtcNow - ticket.LastActivityAt.Value > autoCloseTime.Value)
            {
                if (await guild.GetChannelAsync(ticket.ChannelId) is ITextChannel channel)
                {
                    var embed = new EmbedBuilder()
                        .WithTitle("Ticket Auto-Closed")
                        .WithDescription("This ticket has been automatically closed due to inactivity.")
                        .WithColor(Color.Red)
                        .Build();

                    await channel.SendMessageAsync(embed: embed);
                }

                ticket.ClosedAt = DateTime.UtcNow;
                await ArchiveTicketAsync(ticket);
            }
        }

        await ctx.SaveChangesAsync();
    }

    /// <summary>
    ///     Gets active tickets for a user in a guild.
    /// </summary>
    public async Task<List<Ticket>> GetActiveTicketsAsync(ulong guildId, ulong userId, int id)
    {
        await using var ctx = await _db.GetContextAsync();

        return await ctx.Tickets
            .Where(t => t.GuildId == guildId &&
                t.CreatorId == userId &&
                !t.IsArchived &&
                !t.ClosedAt.HasValue && t.SelectOptionId == id || t.ButtonId == id)
            .ToListAsync();
    }

    /// <summary>
    ///     Sets the priority for a ticket.
    /// </summary>
    public async Task SetTicketPriorityAsync(Ticket ticket, string priority, IGuildUser staff)
    {
        await using var ctx = await _db.GetContextAsync();

        var allowedPriorities = ticket.Button?.AllowedPriorities ??
                                ticket.SelectOption?.AllowedPriorities ?? new List<string>();
        if (allowedPriorities.Any() && !allowedPriorities.Contains(priority))
            throw new InvalidOperationException("Invalid priority level");

        ctx.Attach(ticket);
        ticket.Priority = priority;
        ticket.LastActivityAt = DateTime.UtcNow;
        IGuild guild = _client.GetGuild(ticket.GuildId);

        if (await guild.GetChannelAsync(ticket.ChannelId) is ITextChannel channel)
        {
            var embed = new EmbedBuilder()
                .WithTitle("Ticket Priority Updated")
                .WithDescription($"Priority set to **{priority}** by {staff.Mention}")
                .WithColor(Color.Blue)
                .Build();

            await channel.SendMessageAsync(embed: embed);
        }

        await ctx.SaveChangesAsync();
    }

    /// <summary>
    ///     Adds tags to a ticket.
    /// </summary>
    public async Task AddTicketTagsAsync(Ticket ticket, IEnumerable<string> tags)
    {
        await using var ctx = await _db.GetContextAsync();

        ctx.Attach(ticket);
        ticket.Tags ??= new List<string>();
        ticket.Tags.AddRange(tags);
        ticket.LastActivityAt = DateTime.UtcNow;

        await ctx.SaveChangesAsync();
    }

    /// <summary>
    ///     Checks response times for tickets and sends notifications if needed.
    /// </summary>
    public async Task CheckResponseTimesAsync()
    {
        await using var ctx = await _db.GetContextAsync();

        var tickets = await ctx.Tickets
            .Include(t => t.Button)
            .Include(t => t.SelectOption)
            .Where(t => !t.IsArchived &&
                        !t.ClosedAt.HasValue &&
                        !t.ClaimedBy.HasValue)
            .ToListAsync();

        foreach (var ticket in tickets)
        {
            var requiredResponseTime = ticket.Button?.RequiredResponseTime ?? ticket.SelectOption?.RequiredResponseTime;
            if (!requiredResponseTime.HasValue || !ticket.LastActivityAt.HasValue)
                continue;

            if (DateTime.UtcNow - ticket.CreatedAt > requiredResponseTime.Value)
            {
                // TODO: Send notifications to staff
                Log.Warning("Ticket {TicketId} has exceeded response time threshold", ticket.Id);
            }
        }
    }

    /// <summary>
    ///     Sets the transcript channel for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="channelId">The ID of the channel to set as the transcript channel.</param>
    public async Task SetTranscriptChannelAsync(ulong guildId, ulong channelId)
    {
        await using var ctx = await _db.GetContextAsync();
        var settings = await ctx.GuildTicketSettings.FirstOrDefaultAsync(x => x.GuildId == guildId) ??
                       new GuildTicketSettings();

        settings.TranscriptChannelId = channelId;
        ctx.GuildTicketSettings.Update(settings);
        await ctx.SaveChangesAsync();
    }

    /// <summary>
    ///     Sets the log channel for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="channelId">The ID of the channel to set as the log channel.</param>
    public async Task SetLogChannelAsync(ulong guildId, ulong channelId)
    {
        await using var ctx = await _db.GetContextAsync();
        var settings = await ctx.GuildTicketSettings.FirstOrDefaultAsync(x => x.GuildId == guildId) ??
                       new GuildTicketSettings();

        settings.LogChannelId = channelId;
        ctx.GuildTicketSettings.Update(settings);
        await ctx.SaveChangesAsync();
    }


    /// <summary>
    ///     Links tickets to an existing case.
    /// </summary>
    /// <param name="caseId">The ID of the case to link tickets to.</param>
    /// <param name="tickets">The tickets to link to the case.</param>
    public async Task LinkTicketsToCase(int caseId, IEnumerable<Ticket> tickets)
    {
        await using var ctx = await _db.GetContextAsync();
        var ticketCase = await ctx.TicketCases.FindAsync(caseId);
        if (ticketCase == null) throw new InvalidOperationException("Case not found.");

        foreach (var ticket in tickets)
        {
            ctx.Attach(ticket);
            ticket.CaseId = caseId;
        }

        await ctx.SaveChangesAsync();
    }

    /// <summary>
    ///     Retrieves a ticket by its ID.
    /// </summary>
    /// <param name="ticketId">The ID of the ticket.</param>
    /// <returns>The ticket object, if found.</returns>
    public async Task<Ticket> GetTicketAsync(int ticketId)
    {
        await using var ctx = await _db.GetContextAsync();
        return await ctx.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId);
    }

    /// <summary>
    ///     Retrieves tickets by their IDs.
    /// </summary>
    /// <param name="ticketIds">The IDs of the tickets to retrieve.</param>
    /// <returns>A list of tickets matching the specified IDs.</returns>
    public async Task<List<Ticket>> GetTicketsAsync(IEnumerable<int> ticketIds)
    {
        await using var ctx = await _db.GetContextAsync();
        return await ctx.Tickets.Where(t => ticketIds.Contains(t.Id)).ToListAsync();
    }

    /// <summary>
    ///     Retrieves a panel button by its custom ID.
    /// </summary>
    /// <param name="buttonId">The custom ID of the button to retrieve.</param>
    /// <returns>The panel button matching the specified ID, if found.</returns>
    public async Task<PanelButton?> GetButtonAsync(string buttonId)
    {
        await using var ctx = await _db.GetContextAsync();
        return await ctx.PanelButtons
            .Include(b => b.Panel) // Include the related panel if needed
            .FirstOrDefaultAsync(b => b.CustomId == buttonId);
    }

    /// <summary>
    ///     Gets a ticket panel by ID.
    /// </summary>
    /// <param name="panelId">The ID of the panel to retrieve.</param>
    /// <returns>The panel if found, null otherwise.</returns>
    public async Task<TicketPanel?> GetPanelAsync(string panelId)
    {
        await using var ctx = await _db.GetContextAsync();
        return await ctx.TicketPanels.FindAsync(int.Parse(panelId));
    }

    /// <summary>
    ///     Adds a select menu to an existing ticket panel.
    /// </summary>
    /// <param name="panel">The panel to add the select menu to.</param>
    /// <param name="placeholder">The placeholder text for the menu.</param>
    /// <param name="minValues">Minimum number of selections required.</param>
    /// <param name="maxValues">Maximum number of selections allowed.</param>
    public async Task<PanelSelectMenu> AddSelectMenuAsync(
        TicketPanel panel,
        string placeholder,
        int minValues = 1,
        int maxValues = 1)
    {
        await using var ctx = await _db.GetContextAsync();

        var menu = new PanelSelectMenu
        {
            PanelId = panel.Id, CustomId = $"ticket_select_{Guid.NewGuid():N}", Placeholder = placeholder
        };

        ctx.Attach(panel);
        panel.SelectMenus.Add(menu);
        await UpdatePanelComponentsAsync(panel);
        await ctx.SaveChangesAsync();

        return menu;
    }

    /// <summary>
    ///     Updates a select menu's properties.
    /// </summary>
    /// <param name="menu">The menu to update.</param>
    /// <param name="updateAction">Action containing the updates to apply.</param>
    public async Task UpdateSelectMenuAsync(PanelSelectMenu menu, Action<PanelSelectMenu> updateAction)
    {
        await using var ctx = await _db.GetContextAsync();

        ctx.Attach(menu);
        updateAction(menu);

        await UpdatePanelComponentsAsync(menu.Panel);
        await ctx.SaveChangesAsync();
    }

    /// <summary>
    ///     Adds an option to a select menu.
    /// </summary>
    /// <param name="menu">The menu to add the option to.</param>
    /// <param name="label">The option label.</param>
    /// <param name="value">The option value.</param>
    /// <param name="description">Optional description for the option.</param>
    /// <param name="emoji">Optional emoji for the option.</param>
    /// <param name="openMessageJson">Optional JSON for ticket opening message.</param>
    /// <param name="modalJson">Optional JSON for ticket creation modal.</param>
    /// <param name="channelFormat">Format for ticket channel names.</param>
    /// <param name="categoryId">Optional category for ticket channels.</param>
    /// <param name="archiveCategoryId">Optional category for archived tickets.</param>
    /// <param name="supportRoles">List of support role IDs.</param>
    /// <param name="viewerRoles">List of viewer role IDs.</param>
    /// <param name="autoCloseTime">Optional auto-close duration.</param>
    /// <param name="requiredResponseTime">Optional required response time.</param>
    /// <param name="maxActiveTickets">Maximum active tickets per user.</param>
    /// <param name="allowedPriorities">List of allowed priority IDs.</param>
    /// <param name="defaultPriority">Optional default priority.</param>
    public async Task<SelectMenuOption> AddSelectOptionAsync(
        PanelSelectMenu menu,
        string label,
        string value,
        string description = null,
        string emoji = null,
        string openMessageJson = null,
        string modalJson = null,
        string channelFormat = "ticket-{username}-{id}",
        ulong? categoryId = null,
        ulong? archiveCategoryId = null,
        List<ulong> supportRoles = null,
        List<ulong> viewerRoles = null,
        TimeSpan? autoCloseTime = null,
        TimeSpan? requiredResponseTime = null,
        int maxActiveTickets = 1,
        List<string> allowedPriorities = null,
        string defaultPriority = null)
    {
        await using var ctx = await _db.GetContextAsync();

        var option = new SelectMenuOption
        {
            SelectMenuId = menu.Id,
            Label = label,
            Value = value,
            Description = description,
            Emoji = emoji,
            OpenMessageJson = openMessageJson,
            ModalJson = modalJson,
            ChannelNameFormat = channelFormat,
            CategoryId = categoryId,
            ArchiveCategoryId = archiveCategoryId,
            SupportRoles = supportRoles ?? new List<ulong>(),
            ViewerRoles = viewerRoles ?? new List<ulong>(),
            AutoCloseTime = autoCloseTime,
            RequiredResponseTime = requiredResponseTime,
            MaxActiveTickets = maxActiveTickets,
            AllowedPriorities = allowedPriorities ?? new List<string>(),
            DefaultPriority = defaultPriority
        };

        ctx.Attach(menu);
        menu.Options.Add(option);
        await UpdatePanelComponentsAsync(menu.Panel);
        await ctx.SaveChangesAsync();

        return option;
    }

    /// <summary>
    ///     Updates a select menu option's properties.
    /// </summary>
    /// <param name="option">The option to update.</param>
    /// <param name="updateAction">Action containing the updates to apply.</param>
    public async Task UpdateSelectOptionAsync(SelectMenuOption option, Action<SelectMenuOption> updateAction)
    {
        await using var ctx = await _db.GetContextAsync();

        ctx.Attach(option);
        updateAction(option);

        await UpdatePanelComponentsAsync(option.SelectMenu.Panel);
        await ctx.SaveChangesAsync();
    }

    /// <summary>
    ///     Retrieves a select menu by its custom ID.
    /// </summary>
    /// <param name="menuId">The custom ID of the menu to retrieve.</param>
    /// <returns>The select menu matching the specified ID, if found.</returns>
    public async Task<PanelSelectMenu> GetSelectMenuAsync(string menuId)
    {
        await using var ctx = await _db.GetContextAsync();
        return await ctx.PanelSelectMenus
            .Include(m => m.Panel)
            .Include(m => m.Options)
            .FirstOrDefaultAsync(m => m.CustomId == menuId);
    }

    /// <summary>
    ///     Retrieves a select menu option by its value.
    /// </summary>
    /// <param name="menuId">The ID of the menu containing the option.</param>
    /// <param name="value">The value of the option to retrieve.</param>
    /// <returns>The select menu option matching the specified value, if found.</returns>
    public async Task<SelectMenuOption> GetSelectOptionAsync(int menuId, string value)
    {
        await using var ctx = await _db.GetContextAsync();
        return await ctx.SelectMenuOptions
            .Include(o => o.SelectMenu)
            .FirstOrDefaultAsync(o => o.SelectMenuId == menuId && o.Value == value);
    }
}
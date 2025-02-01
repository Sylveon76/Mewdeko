using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Mewdeko.Database.Models;

/// <summary>
/// Represents a ticket panel that can contain buttons and select menus for ticket creation.
/// </summary>
public class TicketPanel
{
    /// <summary>
    /// Gets or sets the unique identifier for the panel.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the guild where this panel is located.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the channel where this panel is displayed.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the message containing this panel.
    /// </summary>
    public ulong MessageId { get; set; }

    /// <summary>
    /// Gets or sets the JSON configuration for the panel's embed using SmartEmbed format.
    /// </summary>
    public string EmbedJson { get; set; }

    /// <summary>
    /// Gets or sets the collection of buttons associated with this panel.
    /// </summary>
    public List<PanelButton>? Buttons { get; set; } = new();

    /// <summary>
    /// Gets or sets the collection of select menus associated with this panel.
    /// </summary>
    public List<PanelSelectMenu>? SelectMenus { get; set; } = new();
}

/// <summary>
/// Represents a button component within a ticket panel that can create tickets when clicked.
/// </summary>
public class PanelButton
{
    /// <summary>
    /// Gets or sets the unique identifier for the button.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the panel this button belongs to.
    /// </summary>
    public int PanelId { get; set; }

    /// <summary>
    /// Gets or sets the panel this button belongs to.
    /// </summary>
    public TicketPanel Panel { get; set; }

    /// <summary>
    /// Gets or sets the label text displayed on the button.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the optional emoji displayed on the button.
    /// </summary>
    public string? Emoji { get; set; }

    /// <summary>
    /// Gets or sets the custom ID used to identify button interactions.
    /// </summary>
    public string CustomId { get; set; }

    /// <summary>
    /// Gets or sets the visual style of the button.
    /// </summary>
    public ButtonStyle Style { get; set; }

    /// <summary>
    /// Gets or sets the optional JSON configuration for the ticket's opening message using SmartEmbed format.
    /// </summary>
    public string? OpenMessageJson { get; set; }

    /// <summary>
    /// Gets or sets the optional JSON configuration for the modal shown when creating a ticket.
    /// </summary>
    public string? ModalJson { get; set; }

    /// <summary>
    /// Gets or sets the format string for generated ticket channel names.
    /// Supports placeholders: {username}, {id}
    /// </summary>
    public string ChannelNameFormat { get; set; } = "ticket-{username}-{id}";

    /// <summary>
    /// Gets or sets the optional category ID where ticket channels will be created.
    /// </summary>
    public ulong? CategoryId { get; set; }

    /// <summary>
    /// Gets or sets the optional category ID where tickets will be moved when archived.
    /// </summary>
    public ulong? ArchiveCategoryId { get; set; }

    /// <summary>
    /// Gets or sets the list of role IDs that have full access to tickets created by this button.
    /// </summary>
    public List<ulong>? SupportRoles { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of role IDs that can view but not interact with tickets created by this button.
    /// </summary>
    public List<ulong>? ViewerRoles { get; set; } = new();

    /// <summary>
    /// Gets or sets the optional duration after which tickets will automatically close due to inactivity.
    /// </summary>
    public TimeSpan? AutoCloseTime { get; set; }

    /// <summary>
    /// Gets or sets the optional duration within which staff should respond to tickets.
    /// </summary>
    public TimeSpan? RequiredResponseTime { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of active tickets a user can have through this button.
    /// Default is 1.
    /// </summary>
    public int MaxActiveTickets { get; set; } = 1;

    /// <summary>
    /// Gets or sets the list of priority IDs that can be assigned to tickets created by this button.
    /// </summary>
    public List<string>? AllowedPriorities { get; set; } = new();

    /// <summary>
    /// Gets or sets the optional default priority ID for tickets created by this button.
    /// </summary>
    public string? DefaultPriority { get; set; }

    /// <summary>
    /// Gets or sets whether transcripts are saved for this ticket type
    /// </summary>
    public bool SaveTranscript { get; set; } = true;
}

/// <summary>
/// Represents a select menu component within a ticket panel that provides multiple ticket creation options.
/// </summary>
public class PanelSelectMenu
{
    /// <summary>
    /// Gets or sets the unique identifier for the select menu.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the panel this select menu belongs to.
    /// </summary>
    public int PanelId { get; set; }

    /// <summary>
    /// Gets or sets the panel this select menu belongs to.
    /// </summary>
    public TicketPanel Panel { get; set; }

    /// <summary>
    /// Gets or sets the custom ID used to identify select menu interactions.
    /// </summary>
    public string CustomId { get; set; }

    /// <summary>
    /// Gets or sets the placeholder text displayed when no option is selected.
    /// </summary>
    public string Placeholder { get; set; }

    /// <summary>
    /// Gets or sets the collection of options available in this select menu.
    /// </summary>
    public List<SelectMenuOption> Options { get; set; } = new();
}

/// <summary>
/// Represents an option within a select menu that can create tickets when selected.
/// </summary>
public class SelectMenuOption
{
    /// <summary>
    /// Gets or sets the unique identifier for the option.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the select menu this option belongs to.
    /// </summary>
    public int SelectMenuId { get; set; }

    /// <summary>
    /// Gets or sets the select menu this option belongs to.
    /// </summary>
    public PanelSelectMenu SelectMenu { get; set; }

    /// <summary>
    /// Gets or sets the label text displayed for this option.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the value sent when this option is selected.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Gets or sets the optional description text shown below the label.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the optional emoji displayed alongside the label.
    /// </summary>
    public string? Emoji { get; set; }

    /// <summary>
    /// Gets or sets the optional JSON configuration for the ticket's opening message using SmartEmbed format.
    /// </summary>
    public string? OpenMessageJson { get; set; }

    /// <summary>
    /// Gets or sets the optional JSON configuration for the modal shown when creating a ticket.
    /// </summary>
    public string? ModalJson { get; set; }

    /// <summary>
    /// Gets or sets the format string for generated ticket channel names.
    /// Supports placeholders: {username}, {id}
    /// </summary>
    public string ChannelNameFormat { get; set; } = "ticket-{username}-{id}";

    /// <summary>
    /// Gets or sets the optional category ID where ticket channels will be created.
    /// </summary>
    public ulong? CategoryId { get; set; }

    /// <summary>
    /// Gets or sets the optional category ID where tickets will be moved when archived.
    /// </summary>
    public ulong? ArchiveCategoryId { get; set; }

    /// <summary>
    /// Gets or sets the list of role IDs that have full access to tickets created by this option.
    /// </summary>
    public List<ulong>? SupportRoles { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of role IDs that can view but not interact with tickets created by this option.
    /// </summary>
    public List<ulong>? ViewerRoles { get; set; } = new();

    /// <summary>
    /// Gets or sets the optional duration after which tickets will automatically close due to inactivity.
    /// </summary>
    public TimeSpan? AutoCloseTime { get; set; }

    /// <summary>
    /// Gets or sets the optional duration within which staff should respond to tickets.
    /// </summary>
    public TimeSpan? RequiredResponseTime { get; set; }

    /// <summary>
    ///     Gets or sets whether to save transcripts for this part
    /// </summary>
    public bool? SaveTranscript { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of active tickets a user can have through this option.
    /// Default is 1.
    /// </summary>
    public int MaxActiveTickets { get; set; } = 1;

    /// <summary>
    /// Gets or sets the list of priority IDs that can be assigned to tickets created by this option.
    /// </summary>
    public List<string>? AllowedPriorities { get; set; } = new();

    /// <summary>
    /// Gets or sets the optional default priority ID for tickets created by this option.
    /// </summary>
    public string? DefaultPriority { get; set; }
}

/// <summary>
/// Represents a support ticket created through a panel component.
/// </summary>
public class Ticket
{
    /// <summary>
    /// Gets or sets the unique identifier for the ticket.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the guild where this ticket exists.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the channel created for this ticket.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the user who created this ticket.
    /// </summary>
    public ulong CreatorId { get; set; }

    /// <summary>
    /// Gets or sets the optional ID of the button used to create this ticket.
    /// </summary>
    public int? ButtonId { get; set; }

    /// <summary>
    /// Gets or sets the button used to create this ticket, if applicable.
    /// </summary>
    public PanelButton? Button { get; set; }

    /// <summary>
    /// Gets or sets the optional ID of the select menu option used to create this ticket.
    /// </summary>
    public int? SelectOptionId { get; set; }

    /// <summary>
    /// Gets or sets the select menu option used to create this ticket, if applicable.
    /// </summary>
    public SelectMenuOption? SelectOption { get; set; }

    /// <summary>
    /// Gets or sets the JSON string containing responses from the ticket creation modal, if any.
    /// </summary>
    public string? ModalResponses { get; set; }

    /// <summary>
    /// Gets or sets the date and time when this ticket was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the optional date and time when this ticket was closed.
    /// </summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>
    /// Gets or sets the date and time of the last activity in this ticket.
    /// </summary>
    public DateTime? LastActivityAt { get; set; }

    /// <summary>
    /// Gets or sets the optional priority assigned to this ticket.
    /// </summary>
    public string? Priority { get; set; }

    /// <summary>
    /// Gets or sets the list of tags associated with this ticket.
    /// </summary>
    public List<string>? Tags { get; set; } = new();

    /// <summary>
    /// Gets or sets the optional ID of the staff member who claimed this ticket.
    /// </summary>
    public ulong? ClaimedBy { get; set; }

    /// <summary>
    /// Gets or sets the collection of notes added to this ticket.
    /// </summary>
    public List<TicketNote>? Notes { get; set; } = new();

    /// <summary>
    /// Gets or sets the optional ID of the case this ticket is linked to.
    /// </summary>
    public int? CaseId { get; set; }

    /// <summary>
    /// Gets or sets the case this ticket is linked to, if any.
    /// </summary>
    public TicketCase? Case { get; set; }

    /// <summary>
    /// Gets or sets whether this ticket has been archived.
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// Gets or sets the optional URL to this ticket's transcript.
    /// </summary>
    public string? TranscriptUrl { get; set; }
}

/// <summary>
/// Represents a case that groups related tickets together for organizational purposes.
/// </summary>
public class TicketCase
{
    /// <summary>
    /// Gets or sets the unique identifier for the case.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the guild where this case exists.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the title of the case.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the optional detailed description of the case.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the ID of the user who created this case.
    /// </summary>
    public ulong CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the date and time when this case was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the optional date and time when this case was closed.
    /// </summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>
    /// Gets or sets the collection of tickets linked to this case.
    /// </summary>
    public List<Ticket> LinkedTickets { get; set; } = new();

    /// <summary>
    /// Gets or sets the collection of notes added to this case.
    /// </summary>
    public List<CaseNote> Notes { get; set; } = new();
}

/// <summary>
/// Represents a note added to a ticket by staff members.
/// </summary>
public class TicketNote
{
    /// <summary>
    /// Gets or sets the unique identifier for the note.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the ticket this note belongs to.
    /// </summary>
    public int TicketId { get; set; }

    /// <summary>
    /// Gets or sets the ticket this note belongs to.
    /// </summary>
    public Ticket Ticket { get; set; }

    /// <summary>
    /// Gets or sets the ID of the user who authored this note.
    /// </summary>
    public ulong AuthorId { get; set; }

    /// <summary>
    /// Gets or sets the content of the note.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets the date and time when this note was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the collection of edits made to this note.
    /// </summary>
    public List<NoteEdit> EditHistory { get; set; } = new();
}

/// <summary>
/// Represents a note added to a case by staff members.
/// </summary>
public class CaseNote
{
    /// <summary>
    /// Gets or sets the unique identifier for the note.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the case this note belongs to.
    /// </summary>
    public int CaseId { get; set; }

    /// <summary>
    /// Gets or sets the case this note belongs to.
    /// </summary>
    public TicketCase Case { get; set; }

    /// <summary>
    /// Gets or sets the ID of the user who authored this note.
    /// </summary>
    public ulong AuthorId { get; set; }

    /// <summary>
    /// Gets or sets the content of the note.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets the date and time when this note was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the collection of edits made to this note.
    /// </summary>
    public List<NoteEdit> EditHistory { get; set; } = new();
}

/// <summary>
/// Represents an edit made to a ticket or case note.
/// </summary>
public class NoteEdit
{
    /// <summary>
    /// Gets or sets the unique identifier for the edit.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the content of the note before the edit.
    /// </summary>
    public string OldContent { get; set; }

    /// <summary>
    /// Gets or sets the content of the note after the edit.
    /// </summary>
    public string NewContent { get; set; }

    /// <summary>
    /// Gets or sets the ID of the user who made the edit.
    /// </summary>
    public ulong EditorId { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the edit was made.
    /// </summary>
    public DateTime EditedAt { get; set; }
}

/// <summary>
/// Represents the ticket system configuration for a guild.
/// </summary>
public class GuildTicketSettings
{
    /// <summary>
    /// Gets or sets the unique identifier for the settings.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the guild these settings belong to.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the default duration of inactivity before tickets auto-close.
    /// </summary>
    public TimeSpan? DefaultAutoCloseTime { get; set; }

    /// <summary>
    /// Gets or sets the default duration within which staff should respond to tickets.
    /// </summary>
    public TimeSpan? DefaultResponseTime { get; set; }

    /// <summary>
    /// Gets or sets the default maximum number of active tickets per user.
    /// Default is 1.
    /// </summary>
    public int DefaultMaxTickets { get; set; } = 1;

    /// <summary>
    /// Gets or sets the optional channel ID where ticket-related actions are logged.
    /// </summary>
    public ulong? LogChannelId { get; set; }

    /// <summary>
    /// Gets or sets the optional channel ID where ticket transcripts are stored.
    /// </summary>
    public ulong? TranscriptChannelId { get; set; }

    /// <summary>
    /// Gets or sets the list of user IDs that are blocked from creating tickets.
    /// </summary>
    public List<ulong>? BlacklistedUsers { get; set; } = [];

    /// <summary>
    /// Gets or sets the raw JSON string storing blacklisted ticket types per user.
    /// </summary>
    private string _blacklistedTypesJson;

    /// <summary>
    /// Gets or sets the dictionary mapping user IDs to their blacklisted ticket types.
    /// </summary>
    [NotMapped]
    public Dictionary<ulong, List<string>> BlacklistedTypes
    {
        get => string.IsNullOrEmpty(_blacklistedTypesJson)
            ? new Dictionary<ulong, List<string>>()
            : JsonSerializer.Deserialize<Dictionary<ulong, List<string>>>(_blacklistedTypesJson);
        set => _blacklistedTypesJson = JsonSerializer.Serialize(value);
    }

    /// <summary>
    /// Gets or sets whether staff role pings are enabled for tickets.
    /// Default is true.
    /// </summary>
    public bool EnableStaffPings { get; set; } = true;

    /// <summary>
    /// Gets or sets whether DM notifications are enabled for staff.
    /// Default is true.
    /// </summary>
    public bool EnableDmNotifications { get; set; } = true;

    /// <summary>
    /// Gets or sets the list of role IDs that should receive ticket notifications.
    /// </summary>
    public List<ulong> NotificationRoles { get; set; } = new();
}
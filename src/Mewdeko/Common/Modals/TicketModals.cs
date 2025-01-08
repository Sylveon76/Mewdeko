using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
///     Modal for creating a new ticket panel.
/// </summary>
public class PanelCreationModal : IModal
{
    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title => "Create Ticket Panel";

    /// <summary>
    ///     Gets or sets the SmartEmbed JSON configuration for the panel.
    /// </summary>
    [InputLabel("Embed Configuration")]
    [ModalTextInput("embed_json", TextInputStyle.Paragraph, "Enter the SmartEmbed JSON configuration")]
    public string EmbedJson { get; set; }
}

/// <summary>
///     Modal for creating a new ticket button.
/// </summary>
public class ButtonCreationModal : IModal
{
    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title => "Create Ticket Button";

    /// <summary>
    ///     Gets or sets the SmartEmbed JSON for the ticket opening message.
    /// </summary>
    [InputLabel("Open Message")]
    [ModalTextInput("open_message_json", TextInputStyle.Paragraph, "SmartEmbed JSON for ticket opening message")]
    public string OpenMessageJson { get; set; }

    /// <summary>
    ///     Gets or sets the JSON configuration for the creation modal.
    /// </summary>
    [InputLabel("Modal Configuration")]
    [ModalTextInput("modal_json", TextInputStyle.Paragraph, "JSON configuration for the creation modal")]
    public string ModalJson { get; set; }

    /// <summary>
    ///     Gets or sets the format string for generated ticket channel names.
    /// </summary>
    [InputLabel("Channel Name Format")]
    [ModalTextInput("channel_format", TextInputStyle.Short, "Format: {username}, {id}", initValue: "ticket-{username}-{id}")]
    public string ChannelFormat { get; set; }

    /// <summary>
    ///     Gets or sets the JSON containing button settings.
    /// </summary>
    [InputLabel("Settings")]
    [ModalTextInput("settings", TextInputStyle.Paragraph, "JSON: autoCloseHours, responseTimeHours, maxTickets")]
    public string Settings { get; set; }
}

/// <summary>
///     Modal for creating a new select menu.
/// </summary>
public class SelectMenuCreationModal : IModal
{
    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title => "Create Select Menu";

    /// <summary>
    ///     Gets or sets the placeholder text shown when no option is selected.
    /// </summary>
    [InputLabel("Placeholder Text")]
    [ModalTextInput("placeholder", TextInputStyle.Short, "Text shown when no option is selected")]
    public string Placeholder { get; set; }
}

/// <summary>
///     Modal for creating a new select menu option.
/// </summary>
public class SelectOptionCreationModal : IModal
{
    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title => "Create Select Option";

    /// <summary>
    ///     Gets or sets the label text for the option.
    /// </summary>
    [InputLabel("Label")]
    [ModalTextInput("label", TextInputStyle.Short, "Option label text")]
    public string Label { get; set; }

    /// <summary>
    ///     Gets or sets the description text for the option.
    /// </summary>
    [InputLabel("Description")]
    [ModalTextInput("description", TextInputStyle.Short, "Option description")]
    public string Description { get; set; }

    /// <summary>
    ///     Gets or sets the JSON containing option settings.
    /// </summary>
    [InputLabel("Settings")]
    [ModalTextInput("settings", TextInputStyle.Paragraph, "Same settings as button configuration")]
    public string Settings { get; set; }
}

/// <summary>
///     Modal for creating a new ticket case.
/// </summary>
public class CaseCreationModal : IModal
{
    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title => "Create Case";

    /// <summary>
    ///     Gets or sets the title of the case.
    /// </summary>
    [InputLabel("Title")]
    [ModalTextInput("title", TextInputStyle.Short, "Case title")]
    public string CaseTitle { get; set; }

    /// <summary>
    ///     Gets or sets the description of the case.
    /// </summary>
    [InputLabel("Description")]
    [ModalTextInput("description", TextInputStyle.Paragraph, "Case description")]
    public string Description { get; set; }
}

/// <summary>
///     Modal for adding a note to a ticket.
/// </summary>
public class TicketNoteModal : IModal
{
    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title => "Add Note";

    /// <summary>
    ///     Gets or sets the content of the note.
    /// </summary>
    [InputLabel("Note Content")]
    [ModalTextInput("content", TextInputStyle.Paragraph, "Enter your note")]
    public string Content { get; set; }
}

/// <summary>
///     Modal for setting a ticket's priority.
/// </summary>
public class TicketPriorityModal : IModal
{
    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title => "Set Priority";

    /// <summary>
    ///     Gets or sets the priority level.
    /// </summary>
    [InputLabel("Priority")]
    [ModalTextInput("priority", TextInputStyle.Short, "Enter the priority level")]
    public string Priority { get; set; }

    /// <summary>
    ///     Gets or sets the reason for the priority change.
    /// </summary>
    [InputLabel("Reason")]
    [ModalTextInput("reason", TextInputStyle.Paragraph, "Reason for setting this priority")]
    public string Reason { get; set; }
}

/// <inheritdoc />
public class PanelUpdateModal : IModal
{
    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title => "Update Panel";

    /// <summary>
    ///     Gets or sets the SmartEmbed JSON configuration for the panel.
    /// </summary>
    [InputLabel("New Embed Configuration")]
    [ModalTextInput("embed_json", TextInputStyle.Paragraph, "Enter the new SmartEmbed JSON configuration")]
    public string EmbedJson { get; set; }
}

/// <summary>
///     Modal for updating case details.
/// </summary>
public class CaseUpdateModal : IModal
{
    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title => "Update Case";

    /// <summary>
    ///     Gets or sets the new title for the case.
    /// </summary>
    [InputLabel("Case Title")]
    [ModalTextInput("title", TextInputStyle.Short, "Enter new title")]
    public string CaseTitle { get; set; }

    /// <summary>
    ///     Gets or sets the new description for the case.
    /// </summary>
    [InputLabel("Description")]
    [ModalTextInput("description", TextInputStyle.Paragraph, "Enter new description")]
    public string Description { get; set; }
}

/// <summary>
///     Modal for adding a note to a case.
/// </summary>
public class CaseNoteModal : IModal
{
    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title => "Add Case Note";

    /// <summary>
    ///     Gets or sets the content of the note.
    /// </summary>
    [InputLabel("Note Content")]
    [ModalTextInput("content", TextInputStyle.Paragraph, "Enter your note", minLength: 1, maxLength: 1000)]
    public string Content { get; set; }
}


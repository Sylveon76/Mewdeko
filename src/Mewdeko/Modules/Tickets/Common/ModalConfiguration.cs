namespace Mewdeko.Modules.Tickets.Common;

/// <summary>
/// Represents the configuration for a Discord modal used in ticket creation.
/// </summary>
/// <remarks>
/// This class combines both the modal's title and its field configurations,
/// allowing for complete customization of ticket creation forms.
/// </remarks>
public class ModalConfiguration
{
    /// <summary>
    /// Gets or sets the title displayed at the top of the modal.
    /// </summary>
    /// <remarks>
    /// Defaults to "Create Ticket" if not explicitly set.
    /// This title is shown to users when they interact with a ticket button.
    /// </remarks>
    public string Title { get; set; } = "Create Ticket";

    /// <summary>
    /// Gets or sets the dictionary of fields in the modal.
    /// </summary>
    /// <remarks>
    /// The dictionary keys are the field identifiers, and the values are their configurations.
    /// A modal can have up to 5 fields as per Discord's limitations.
    /// </remarks>
    public Dictionary<string, ModalFieldConfig> Fields { get; set; } = new();
}

/// <summary>
/// Represents configuration for a modal field in a ticket creation form.
/// </summary>
public class ModalFieldConfig
{
    /// <summary>
    /// Gets or sets the label text shown above the field.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the input style (1 for short, 2 for paragraph).
    /// </summary>
    public int Style { get; set; }

    /// <summary>
    /// Gets or sets whether the field is required.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets the minimum length for the input.
    /// </summary>
    public int? MinLength { get; set; }

    /// <summary>
    /// Gets or sets the maximum length for the input.
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// Gets or sets the placeholder text shown when no input is provided.
    /// </summary>
    public string Placeholder { get; set; }

    /// <summary>
    /// Gets or sets the default value of the field.
    /// </summary>
    public string Value { get; set; }
}
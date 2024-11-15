using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
///     Simple modal for single-input prompts.
/// </summary>
public class SimpleInputModal : IModal
{
    /// <summary>
    ///     Gets the modal title.
    /// </summary>
    public string Title => "Input Required";

    /// <summary>
    ///     Gets or sets the input value.
    /// </summary>
    [InputLabel("Input")]
    [ModalTextInput("input")]
    public string Input { get; set; }
}
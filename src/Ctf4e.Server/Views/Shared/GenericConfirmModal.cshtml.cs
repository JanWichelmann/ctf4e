using System.Collections.Generic;

namespace Ctf4e.Server.Views.Shared;

public class GenericConfirmModalModel
{
    /// <summary>
    /// ID of the modal dialog.
    /// Bootstrap takes this ID to trigger the modal.
    /// </summary>
    public required string ModalId { get; set; }

    /// <summary>
    /// JavaScript callback function to invoke on submit.
    /// </summary>
    public required string CallbackFunctionName { get; set; }

    /// <summary>
    /// Modal title.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Messages shown in the modal body.
    /// </summary>
    public required List<string> Messages { get; set; } = [];
    
    /// <summary>
    /// Text of the submit button.
    /// </summary>
    public required string SubmitButtonText { get; set; }

    /// <summary>
    /// Color scheme of the modal.
    /// </summary>
    public required ColorScheme Color { get; set; }

    public enum ColorScheme
    {
        Danger
    }
}
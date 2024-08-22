using System.Collections.Generic;

namespace Ctf4e.Server.Views.Shared;

public class ConfirmDeletionDialogModel
{
    /// <summary>
    /// ID of the modal dialog.
    /// Bootstrap takes this ID to trigger the modal.
    /// </summary>
    public required string ModalId { get; set; }

    /// <summary>
    /// URL to post the deletion request to.
    /// </summary>
    public required string PostUrl { get; set; }

    /// <summary>
    /// Name of the deleted object type. Is shown in the title bar.
    /// </summary>
    public required string DeletedObjectName { get; set; }

    /// <summary>
    /// Additional information to be shown in the dialog, each entry is a paragraph.
    /// </summary>
    public List<string> AdditionalMessages { get; set; } = [];

    /// <summary>
    /// Form parameters to be included in the POST body.
    /// </summary>
    public required List<(string postName, string buttonDataName)> PostParameters { get; set; } = [];

    /// <summary>
    /// Controls whether this is an undeletion dialog.
    /// </summary>
    public bool IsUndeletion { get; set; }
}
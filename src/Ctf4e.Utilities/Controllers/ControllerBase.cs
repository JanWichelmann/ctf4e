using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace Ctf4e.Utilities.Controllers;

/// <summary>
///     Abstract base class for controllers.
/// </summary>
public abstract class ControllerBase : Controller
{
    private readonly string _viewPath;
    private readonly List<(string Message, StatusMessageTypes Type, bool Preformatted)> _statusMessages;

    protected ControllerBase(string viewPath)
    {
        _viewPath = viewPath;
        _statusMessages = new List<(string, StatusMessageTypes, bool)>();
    }

    /// <summary>
    ///     Sets the displayed status message.
    /// </summary>
    /// <param name="message">The message to be displayed.</param>
    /// <param name="messageType">Sets the status message type.</param>
    /// <param name="preformatted">Controls whether the message is preformatted (i.e., whether it should be rendered in a monospace font).</param>
    protected void AddStatusMessage(string message, StatusMessageTypes messageType, bool preformatted = false)
    {
        _statusMessages.Add((message, messageType, preformatted));
    }

    /// <summary>
    ///     Passes some global variables to the template engine and renders the previously specified view.
    /// </summary>
    /// <param name="model">The model being shown/edited in this view.</param>
    protected virtual IActionResult RenderView(object model = null)
    {
        // Pass status messages
        ViewData["StatusMessages"] = _statusMessages;

        // Render view
        return View(_viewPath, model);
    }
}
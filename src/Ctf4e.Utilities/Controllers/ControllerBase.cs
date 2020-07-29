using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace Ctf4e.Utilities.Controllers
{
    /// <summary>
    ///     Abstract base class for controllers.
    /// </summary>
    public abstract class ControllerBase : Controller
    {
        private readonly string _viewPath;
        private readonly List<(string Message, StatusMessageTypes Type)> _statusMessages;

        protected ControllerBase(string viewPath)
        {
            _viewPath = viewPath;
            _statusMessages = new List<(string, StatusMessageTypes)>();
        }

        /// <summary>
        ///     Sets the displayed status message.
        /// </summary>
        /// <param name="message">The message to be displayed.</param>
        /// <param name="messageType">Sets the status message type.</param>
        protected void AddStatusMessage(string message, StatusMessageTypes messageType)
        {
            _statusMessages.Add((message, messageType));
        }

        /// <summary>
        ///     Passes some global variables to the template engine and renders the previously specified view.
        /// </summary>
        /// <param name="model">The model being shown/edited in this view.</param>
        protected virtual IActionResult RenderView(object model = null)
        {
            // Pass statuts messages
            ViewData["StatusMessages"] = _statusMessages;

            // Render view
            return View(_viewPath, model);
        }
    }
}
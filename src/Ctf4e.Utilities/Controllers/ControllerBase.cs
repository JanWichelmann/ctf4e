using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Ctf4e.Utilities.Controllers;

/// <summary>
/// Abstract base class for controllers.
/// </summary>
public abstract class ControllerBase<TController> : Controller
    where TController : ControllerBase<TController>
{
    protected List<StatusMessage> StatusMessages { get; } = [];

    private IStringLocalizer<TController> _localizer;

    /// <summary>
    /// Returns a string localizer for the current controller.
    /// </summary>
    protected IStringLocalizer<TController> Localizer => _localizer ??= HttpContext.RequestServices.GetRequiredService<IStringLocalizer<TController>>();

    /// <summary>
    /// Stores a single status message in the controller's TempData collection.
    /// The message is displayed at the next request.
    ///
    /// This property can only store a single message.
    /// Use this property exclusively for POST-Redirect-GET, all other status messages
    /// should be stored directly in <see cref="StatusMessages"/>.
    /// </summary>
    protected StatusMessage? PostStatusMessage
    {
        get => TempData.GetJson<StatusMessage>("PostStatusMessage");
        set
        {
            if(value == null)
            {
                if(TempData.ContainsKey("PostStatusMessage"))
                    TempData.Remove("PostStatusMessage");
            }
            else
                TempData.SetJson("PostStatusMessage", value.Value);
        }
    }

    protected void AddStatusMessage(StatusMessageType type, string message)
        => StatusMessages.Add(new StatusMessage(type, message));

    /// <summary>
    /// Retrieves a logger for the current controller.
    /// </summary>
    /// <returns>Logger for the current controller.</returns>
    protected ILogger<TController> GetLogger()
    {
        return HttpContext.RequestServices.GetRequiredService<ILogger<TController>>();
    }

    /// <summary>
    /// Passes some global variables to the template engine and renders the previously specified view.
    /// </summary>
    /// <param name="viewPath">Path to the view file.</param>
    /// <param name="model">The model being shown/edited in this view.</param>
    protected virtual IActionResult RenderView(string viewPath, object model = null)
    {
        // Pass status messages
        if(PostStatusMessage != null)
            StatusMessages.Add(PostStatusMessage.Value);
        ViewData["StatusMessages"] = StatusMessages;

        // Render view
        return View(viewPath, model);
    }
}
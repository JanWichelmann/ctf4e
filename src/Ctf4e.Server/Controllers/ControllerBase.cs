using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Ctf4e.Server.Attributes;
using Ctf4e.Server.Authorization;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Models;
using Ctf4e.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Ctf4e.Server.Controllers;

/// <summary>
/// Abstract base class for controllers.
/// </summary>
public abstract class ControllerBase<TController>(IUserService userService) : Utilities.Controllers.ControllerBase<TController>
    where TController : ControllerBase<TController>
{
    /// <summary>
    /// Currently displayed menu item.
    /// Should be overridden by derived classes.
    /// </summary>
    protected virtual MenuItems ActiveMenuItem => MenuItems.Undefined;

    /// <summary>
    /// Version of this assembly.
    /// </summary>
    private static readonly string _buildVersion;

    private User _currentUser;
    private bool _currentUserHasLoggedOut;

    static ControllerBase()
    {
        
    }

    /// <summary>
    ///     Updates the internal current user variable.
    ///     Internal method, only to be called on login due to the still unset session variable.
    /// </summary>
    /// <param name="userId">The ID of the currently logged in user.</param>
    /// <returns></returns>
    protected async Task HandleUserLoginAsync(int userId)
    {
        _currentUser = await userService.FindUserByIdAsync(userId, HttpContext.RequestAborted);
    }

    /// <summary>
    ///     Clears the internal current user variable.
    ///     Internal method, only to be called on logout due to the still set session variable.
    /// </summary>
    /// <returns></returns>
    protected void HandleUserLogout()
    {
        _currentUser = null;
        _currentUserHasLoggedOut = true;
    }

    /// <summary>
    ///     Updates the internal current user variable.
    /// </summary>
    /// <returns></returns>
    private async Task ReadCurrentUserFromSessionAsync()
    {
        // User authenticated?
        bool isAuthenticated = User.Identities.Any(i => i.IsAuthenticated);
        if(isAuthenticated)
        {
            // Retrieve user data
            int userId = int.Parse(User.Claims.First(c => c.Type == AuthenticationStrings.ClaimUserId).Value);
            _currentUser = await userService.FindUserByIdAsync(userId, HttpContext.RequestAborted);
        }
    }

    /// <summary>
    ///     Returns the user data of the currently authenticated user.
    /// </summary>
    /// <returns></returns>
    protected async Task<User> GetCurrentUserAsync()
    {
        // Cached user data?
        if(_currentUser != null || _currentUserHasLoggedOut)
            return _currentUser;

        await ReadCurrentUserFromSessionAsync();
        return _currentUser;
    }

    /// <summary>
    /// Passes some global variables to the template engine and renders the previously specified view.
    /// </summary>
    /// <param name="viewPath">Path to the view file.</param>
    /// <param name="model">The model being shown/edited in this view.</param>
    protected async Task<IActionResult> RenderViewAsync(string viewPath, object model = null)
    {
        // Pass current user
        var currentUser = await GetCurrentUserAsync();
        ViewData["CurrentUser"] = currentUser;

        // Pass active menu item
        ViewData["ActiveMenuItem"] = ActiveMenuItem;

        // Page title
        // Request service manually, to avoid injecting too many services in constructors of derived classes
        var configService = HttpContext.RequestServices.GetRequiredService<IConfigurationService>();
        ViewData["PageTitle"] = await configService.GetPageTitleAsync(HttpContext.RequestAborted);
        ViewData["NavbarTitle"] = await configService.GetNavbarTitleAsync(HttpContext.RequestAborted);

        // Other render data
        ViewData["BuildVersion"] = _buildVersion;

        // Render view
        return RenderViewInternal(viewPath, model);
    }
}
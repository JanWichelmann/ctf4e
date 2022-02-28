using System;
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
public abstract class ControllerBase : Utilities.Controllers.ControllerBase
{
    /// <summary>
    /// Version of this assembly.
    /// </summary>
    private static string _buildVersion = null;
    
    /// <summary>
    /// ID of the currently logged in user.
    /// </summary>
    private int? _currentUserId = null;

    /// <summary>
    /// Stores whether the current user ID has already been read from the session.
    /// Ensures that the user ID is not read twice, especially after logging out.
    /// </summary>
    private bool _currentUserIdExtractedFromSession = false;

    private readonly IUserService _userService;

    protected ControllerBase(string viewPath, IUserService userService)
        : base(viewPath)
    {
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        
        // Read build version
        if(_buildVersion == null)
        {
            _buildVersion = Assembly.GetExecutingAssembly()
                .GetCustomAttributes<AssemblyBuildVersionAttribute>()
                .FirstOrDefault()?.Version;
            if(string.IsNullOrWhiteSpace(_buildVersion))
                _buildVersion = "DEV";
        }
    }

    /// <summary>
    /// Updates the internal current user variable.
    /// Internal method, only to be called on login due to the still unset session variable.
    /// </summary>
    /// <param name="userId">The ID of the currently logged in user.</param>
    /// <returns></returns>
    protected void HandleUserLogin(int userId)
    {
        _currentUserId = userId;
        _currentUserIdExtractedFromSession = true;
    }

    /// <summary>
    /// Clears the internal current user variable.
    /// Internal method, only to be called on logout due to the still set session variable.
    /// </summary>
    /// <returns></returns>
    protected void HandleUserLogout()
    {
        _currentUserId = null;
        _currentUserIdExtractedFromSession = true;
    }

    /// <summary>
    /// Returns the user data of the currently authenticated user.
    /// </summary>
    /// <returns></returns>
    protected async Task<User> GetCurrentUserAsync()
    {
        if(!_currentUserIdExtractedFromSession)
        {
            // Retrieve ID of currently authenticated user
            bool isAuthenticated = User.Identities.Any(i => i.IsAuthenticated);
            if(isAuthenticated)
                _currentUserId = int.Parse(User.Claims.First(c => c.Type == AuthenticationStrings.ClaimUserId).Value);
			
			_currentUserIdExtractedFromSession = true;
        }
        
        if(_currentUserId == null)
            return null;

        return await _userService.FindUserByIdAsync(_currentUserId.Value, HttpContext.RequestAborted);
    }

    /// <summary>
    /// Passes some global variables to the template engine and renders the previously specified view.
    /// </summary>
    /// <param name="activeMenuItem">The page to be shown as "active" in the menu.</param>
    /// <param name="model">The model being shown/edited in this view.</param>
    protected async Task<IActionResult> RenderViewAsync(MenuItems activeMenuItem = MenuItems.Undefined, object model = null)
    {
        // Pass current user
        var currentUser = await GetCurrentUserAsync();
        ViewData["CurrentUser"] = currentUser;

        // Pass active menu item
        ViewData["ActiveMenuItem"] = activeMenuItem;

        // Page title
        // Request service manually, to avoid injecting too many services in constructors of derived classes
        var configService = HttpContext.RequestServices.GetService<IConfigurationService>() ?? throw new Exception("Could not retrieve configuration service.");
        ViewData["PageTitle"] = await configService.GetPageTitleAsync(HttpContext.RequestAborted);
        ViewData["NavbarTitle"] = await configService.GetNavbarTitleAsync(HttpContext.RequestAborted);

        // Other render data
        ViewData["BuildVersion"] = _buildVersion;

        // Render view
        return RenderView(model);
    }
}
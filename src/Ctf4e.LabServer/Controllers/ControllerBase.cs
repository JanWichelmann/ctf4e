using System.Linq;
using Ctf4e.LabServer.Constants;
using Ctf4e.LabServer.Models;
using Microsoft.AspNetCore.Mvc;

namespace Ctf4e.LabServer.Controllers;

/// <summary>
/// Abstract base class for controllers.
/// </summary>
public abstract class ControllerBase<TController> : Utilities.Controllers.ControllerBase<TController>
    where TController : ControllerBase<TController>
{
    /// <summary>
    /// Currently displayed menu item.
    /// Should be overridden by derived classes.
    /// </summary>
    protected virtual MenuItems ActiveMenuItem => MenuItems.Undefined;
        
    private User _currentUser;
    private bool _currentUserHasLoggedOut;
    private bool _adminMode;

    /// <summary>
    /// Updates the internal current user variable.
    /// Internal method, only to be called on login due to the still unset session variable.
    /// </summary>
    /// <param name="userId">The ID of the currently logged in user.</param>
    /// <param name="userName">The name of the currently logged in user.</param>
    /// <param name="groupId">The ID of the currently logged in group.</param>
    /// <param name="groupName">The name of the currently logged in group.</param>
    /// <param name="adminMode">Defines whether the current user has administrator permissions.</param>
    /// <returns></returns>
    protected void HandleUserLogin(int userId, string userName, int? groupId, string groupName, bool adminMode)
    {
        _currentUser = new User
        {
            UserId = userId,
            UserDisplayName = userName,
            GroupId = groupId,
            GroupName = groupName
        };
        _adminMode = adminMode;
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
        _adminMode = false;
    }

    /// <summary>
    /// Updates the internal current user variable.
    /// </summary>
    /// <returns></returns>
    private void ReadCurrentUserFromSession()
    {
        // Authenticated?
        bool isAuthenticated = User.Identities.Any(i => i.IsAuthenticated);
        if(isAuthenticated)
        {
            // Retrieve user data
            _currentUser = new User
            {
                UserId = int.Parse(User.Claims.First(c => c.Type == AuthenticationStrings.ClaimUserId).Value),
                UserDisplayName = User.Claims.First(c => c.Type == AuthenticationStrings.ClaimUserDisplayName).Value
            };

            var groupIdClaim = User.Claims.FirstOrDefault(c => c.Type == AuthenticationStrings.ClaimGroupId);
            if(groupIdClaim != null)
            {
                _currentUser.GroupId = int.Parse(groupIdClaim.Value);
                _currentUser.GroupName = User.Claims.First(c => c.Type == AuthenticationStrings.ClaimGroupName).Value;
            }
            else
            {
                _currentUser.GroupId = null;
                _currentUser.GroupName = "";
            }

            var adminModeClaim = User.Claims.FirstOrDefault(c => c.Type == AuthenticationStrings.ClaimAdminMode);
            _adminMode = adminModeClaim != null && bool.Parse(adminModeClaim.Value);
        }
    }

    /// <summary>
    /// Returns the user data of the currently authenticated user.
    /// </summary>
    /// <returns></returns>
    protected User GetCurrentUser()
    {
        // Cached user data?
        if(_currentUser != null || _currentUserHasLoggedOut)
            return _currentUser;

        ReadCurrentUserFromSession();
        return _currentUser;
    }

    /// <summary>
    /// Returns whether the currently authenticated user uses admin permissions.
    /// </summary>
    /// <returns></returns>
    protected bool GetAdminMode()
    {
        // Cached user data?
        if(_currentUser == null && !_currentUserHasLoggedOut)
            ReadCurrentUserFromSession();
        return _adminMode;
    }

    /// <summary>
    /// Passes some global variables to the template engine and renders the previously specified view.
    /// </summary>
    /// <param name="viewPath">Path to the view to be rendered.</param>
    /// <param name="model">The model being shown/edited in this view.</param>
    protected override IActionResult RenderView(string viewPath, object model = null)
    {
        // Pass current user
        ViewData["CurrentUser"] = GetCurrentUser();
        ViewData["AdminMode"] = GetAdminMode();

        // Pass active menu item
        ViewData["ActiveMenuItem"] = ActiveMenuItem;

        // Render view
        return base.RenderView(viewPath, model);
    }
}
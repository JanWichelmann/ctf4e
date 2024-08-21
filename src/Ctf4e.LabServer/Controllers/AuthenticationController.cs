using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Ctf4e.Api.Models;
using Ctf4e.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ctf4e.LabServer.Constants;
using Ctf4e.LabServer.Services;
using Ctf4e.Utilities;

namespace Ctf4e.LabServer.Controllers;

[Route("auth")]
public class AuthenticationController(ICryptoService cryptoService, IStateService stateService)
    : ControllerBase<AuthenticationController>
{
    protected override MenuItems ActiveMenuItem => MenuItems.Authentication;

    private IActionResult Render(ViewType viewType, object model = null)
    {
        ViewData["ViewType"] = viewType;
        return RenderView("~/Views/Authentication.cshtml", model);
    }

    [HttpGet]
    public IActionResult Render()
    {
        // Logged in?
        var currentUser = GetCurrentUser();
        if(currentUser == null)
        {
            AddStatusMessage(StatusMessageType.Info, Localizer["Render:AccessDenied"]);
            return Render(ViewType.Blank);
        }

        return Render(ViewType.Redirect);
    }

#if DEBUG
    [HttpGet("login/dev/")]
    public async Task<IActionResult> DevLoginAsync(int userId, string userName, int? groupId, string groupName, bool admin)
    {
        // Already logged in?
        var currentUser = GetCurrentUser();
        if(currentUser != null)
            return Render(ViewType.Redirect);

        // Make sure user account exists
        // Don't allow the user to cancel this too early, but also ensure that the application doesn't block too long
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await stateService.ProcessLoginAsync(userId, groupId, cts.Token);

        // Sign in user
        await DoLoginAsync(userId, userName, groupId, groupName, admin);

        // Done
        AddStatusMessage(StatusMessageType.Success, Localizer["DevLoginAsync:Success"]);
        return Render(ViewType.Redirect);
    }
#endif

    [HttpGet("login")]
    public async Task<IActionResult> LoginAsync(string code)
    {
        // Parse and check request
        var loginData = UserLoginRequest.Deserialize(cryptoService.Decrypt(code));

        // Already logged in?
        var currentUser = GetCurrentUser();
        if(currentUser != null && currentUser.UserId == loginData.UserId && GetAdminMode() == loginData.AdminMode)
            return Render(ViewType.Redirect);

        // Make sure user account exists
        // Don't allow the user to cancel this, but also ensure that the application doesn't block too long
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await stateService.ProcessLoginAsync(loginData.UserId, loginData.GroupId, cts.Token);

        // Sign in user
        await DoLoginAsync(loginData.UserId, loginData.UserDisplayName, loginData.GroupId, loginData.GroupName, loginData.AdminMode);

        // Done
        AddStatusMessage(StatusMessageType.Success, Localizer["LoginAsync:Success"]);
        return Render(ViewType.Redirect);
    }

    private async Task DoLoginAsync(int userId, string userDisplayName, int? groupId, string groupName, bool adminMode)
    {
        // Prepare session data to identify user
        var claims = new List<Claim>
        {
            new(AuthenticationStrings.ClaimUserId, userId.ToString()),
            new(AuthenticationStrings.ClaimUserDisplayName, userDisplayName ?? "")
        };
        if(groupId != null)
        {
            claims.Add(new Claim(AuthenticationStrings.ClaimGroupId, groupId.ToString()));
            claims.Add(new Claim(AuthenticationStrings.ClaimGroupName, groupName ?? ""));
        }

        if(adminMode)
            claims.Add(new Claim(AuthenticationStrings.ClaimAdminMode, true.ToString()));

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        var authProperties = new AuthenticationProperties
        {
            AllowRefresh = true,
            IsPersistent = true
        };

        // Login
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal, authProperties);

        // Make sure the current user is set correctly
        HandleUserLogin(userId, userDisplayName, groupId, groupName, adminMode);
    }

    [HttpGet("logout")]
    [Authorize]
    public async Task<IActionResult> LogoutAsync()
    {
        // Logout
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // Make sure the current user is set correctly
        HandleUserLogout();

        // Done
        AddStatusMessage(StatusMessageType.Success, Localizer["LogoutAsync:Success"]);
        return Render(ViewType.Blank);
    }

    public enum ViewType
    {
        Blank,
        Redirect
    }
}
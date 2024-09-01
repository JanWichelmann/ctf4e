using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Cryptography;
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
using Microsoft.Extensions.Logging;

namespace Ctf4e.LabServer.Controllers;

[Route("auth")]
public class AuthenticationController(ICryptoService cryptoService, IStateService stateService)
    : ControllerBase<AuthenticationController>
{
    protected override MenuItems ActiveMenuItem => MenuItems.Authentication;

    [HttpGet]
    public IActionResult ShowPage()
    {
        // Logged in?
        var currentUser = GetCurrentUser();
        if(currentUser == null)
        {
            AddStatusMessage(StatusMessageType.Info, Localizer["Render:AccessDenied"]);
            return RenderView("~/Views/Authentication/Empty.cshtml");
        }

        return RedirectToAction("ShowDashboard", "Dashboard");
    }

#if DEBUG
    [HttpGet("login/dev/")]
    public async Task<IActionResult> DevLoginAsync(int userId, string userName, int? groupId, string groupName, bool admin)
    {
        // Already logged in?
        var currentUser = GetCurrentUser();
        if(currentUser != null)
            return RedirectToAction("ShowDashboard", "Dashboard");

        // Make sure user account exists
        // Don't allow the user to cancel this too early, but also ensure that the application doesn't block too long
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await stateService.ProcessLoginAsync(userId, groupId, cts.Token);

        // Sign in user
        await DoLoginAsync(userId, userName, groupId, groupName, admin);

        // Done
        PostStatusMessage = new StatusMessage(StatusMessageType.Success, Localizer["DevLoginAsync:Success"]) { AutoHide = true };
        return RedirectToAction("ShowDashboard", "Dashboard");
    }
#endif

    [HttpGet("login")]
    public async Task<IActionResult> LoginAsync(string code)
    {
        try
        {
            // Parse and check request
            var loginData = UserLoginRequest.Deserialize(cryptoService.Decrypt(code));

            // Already logged in?
            var currentUser = GetCurrentUser();
            if(currentUser != null && currentUser.UserId == loginData.UserId && GetAdminMode() == loginData.AdminMode)
                return RedirectToAction("ShowDashboard", "Dashboard");

            // Make sure user account exists
            // Don't allow the user to cancel this, but also ensure that the application doesn't block too long
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await stateService.ProcessLoginAsync(loginData.UserId, loginData.GroupId, cts.Token);

            // Sign in user
            await DoLoginAsync(loginData.UserId, loginData.UserDisplayName, loginData.GroupId, loginData.GroupName, loginData.AdminMode);

            // Done
            PostStatusMessage = new StatusMessage(StatusMessageType.Success, Localizer["LoginAsync:Success"]) { AutoHide = true };
            return RedirectToAction("ShowDashboard", "Dashboard");
        }
        catch(CryptographicException ex)
        {
            GetLogger().LogWarning(ex, "Invalid login code");
            AddStatusMessage(StatusMessageType.Error, Localizer["LoginAsync:InvalidCode"]);
            return RenderView("~/Views/Authentication/Empty.cshtml");
        }
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

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> LogoutAsync()
    {
        // Logout
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // Make sure the current user is set correctly
        HandleUserLogout();

        // Done
        PostStatusMessage = new StatusMessage(StatusMessageType.Success, Localizer["LogoutAsync:Success"]);
        return RedirectToAction("ShowPage");
    }
}
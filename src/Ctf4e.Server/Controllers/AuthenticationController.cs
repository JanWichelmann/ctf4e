using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Ctf4e.Server.Authorization;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Models;
using Ctf4e.Server.Services;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Ctf4e.Server.Controllers;

[Route("auth")]
public partial class AuthenticationController(IUserService userService, IConfigurationService configurationService)
    : ControllerBase<AuthenticationController>(userService)
{
    protected override MenuItems ActiveMenuItem => MenuItems.Authentication;
    
    private readonly IUserService _userService = userService;

    private Task<IActionResult> RenderAsync(ViewType viewType, string viewPath, object model = null)
    {
        ViewData["ViewType"] = viewType;
        return RenderViewAsync(viewPath, model);
    }
    
    [HttpGet]
    public async Task<IActionResult> ShowLoginFormAsync(string returnUrl)
    {
        // Logged in?
        var currentUser = await GetCurrentUserAsync();
        if(currentUser != null) 
            return await ShowRedirectAsync(null); // Redirect to any page the user has access to
        
        if(!string.IsNullOrWhiteSpace(returnUrl))
            ViewData["Referer"] = returnUrl;
        return await RenderAsync(ViewType.Login, "~/Views/Authentication.cshtml");

    }

    private async Task<IActionResult> ShowRedirectAsync(string redirectUrl)
    {
        // Redirect to the given URL. If no URL is specified, try to pick a valid one, depending on the user's privileges
        var currentUser = await GetCurrentUserAsync();
        if(currentUser == null)
            return await ShowLoginFormAsync(null);

        if(redirectUrl == null)
        {
            if(currentUser.Privileges.HasPrivileges(UserPrivileges.ViewAdminScoreboard))
                redirectUrl = Url.Action("RenderScoreboard", "AdminScoreboard");
            else if(currentUser.GroupId == null)
                redirectUrl = Url.Action("ShowGroupForm", "Authentication");
            else
                redirectUrl = Url.Action("RenderLabPage", "UserDashboard");
        }

        ViewData["RedirectUrl"] = redirectUrl;

        return await RenderAsync(ViewType.Redirect, "~/Views/Authentication.cshtml");
    }

#if DEBUG
    [HttpGet("login/dev")]
    public async Task<IActionResult> DevLoginAsync(int userId)
    {
        // Only allow this in development mode
        var webHostEnvironment = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        if(!webHostEnvironment.IsDevelopment())
            return Forbid();
            
        // Already logged in?
        var currentUser = await GetCurrentUserAsync();
        if(currentUser != null)
            return await ShowRedirectAsync(null); // Redirect to any page the user has access to

        // Find user
        var user = await _userService.FindUserByIdAsync(userId, HttpContext.RequestAborted);
        if(user == null)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["DevLoginAsync:NotFound"]);
            return await RenderAsync(ViewType.Login, "~/Views/Authentication.cshtml");
        }

        // Sign in user
        await DoLoginAsync(user);

        // Done
        AddStatusMessage(StatusMessageType.Success, Localizer["DevLoginAsync:Success"]);
        return await ShowRedirectAsync(null); // Redirect to any page the user has access to
    }

    [HttpGet("login/as")]
    public async Task<IActionResult> AdminLoginAsUserAsync(int userId)
    {
        // Try to retrieve user
        var user = await _userService.FindUserByIdAsync(userId, HttpContext.RequestAborted);
        if(user == null)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["AdminLoginAsUserAsync:NotFound"]);
            return await RenderAsync(ViewType.Login, "~/Views/Authentication.cshtml");
        }

        // Sign in again, but as another user
        await DoLoginAsync(user);

        // Done
        AddStatusMessage(StatusMessageType.Success, Localizer["AdminLoginAsUserAsync:Success", user.DisplayName]);
        return await ShowRedirectAsync(null); // Redirect to any page the user has access to
    }
#endif

    private async Task DoLoginAsync(User user)
    {
        // Prepare session data to identify user
        var claims = new List<Claim>
        {
            new (AuthenticationStrings.ClaimUserId, user.Id.ToString())
        };

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        var authProperties = new AuthenticationProperties
        {
            AllowRefresh = true,
            IsPersistent = true
        };

        // Login
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal, authProperties);

        // Make sure the current user is set correctly
        await HandleUserLoginAsync(user.Id);
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
        return await RenderAsync(ViewType.Login, "~/Views/Authentication.cshtml");
    }

    public enum ViewType
    {
        Login,
        GroupSelection,
        Settings,
        Redirect
    }
}
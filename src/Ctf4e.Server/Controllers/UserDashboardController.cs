using System;
using System.Threading.Tasks;
using Ctf4e.Api.Models;
using Ctf4e.Api.Services;
using Ctf4e.Server.Authorization;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Services;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Ctf4e.Server.Controllers;

[Route("dashboard")]
public class UserDashboardController(IUserService userService, IScoreboardService scoreboardService)
    : ControllerBase<UserDashboardController>(userService)
{
    protected override MenuItems ActiveMenuItem => MenuItems.Group;

    private async Task<IActionResult> RenderAsync(int? labId)
    {
        const string viewPath = "~/Views/UserDashboard.cshtml";

        // Retrieve group ID
        var currentUser = await GetCurrentUserAsync();
        if(currentUser?.GroupId == null)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["RenderAsync:NoGroup"]);
            return await RenderViewAsync(viewPath);
        }

        // Show group's most recent lab
        if(labId == null)
        {
            var labExecutionService = HttpContext.RequestServices.GetRequiredService<ILabExecutionService>();
            var currentLabExecution = await labExecutionService.FindMostRecentLabExecutionByGroupAsync(currentUser.GroupId.Value, HttpContext.RequestAborted);
            if(currentLabExecution == null)
            {
                AddStatusMessage(StatusMessageType.Info, Localizer["RenderAsync:NoActiveLab"]);
                return await RenderViewAsync(viewPath);
            }

            labId = currentLabExecution.LabId;
        }

        // Check whether user may access this lab, if it even exists
        var labService = HttpContext.RequestServices.GetRequiredService<ILabService>();
        var lab = await labService.FindLabByIdAsync(labId.Value, HttpContext.RequestAborted);
        if(lab == null || (!lab.Visible && !currentUser.Privileges.HasAnyPrivilege(UserPrivileges.ViewAdminScoreboard | UserPrivileges.ViewLabs)))
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["RenderAsync:LabNotFound"]);
            return await RenderViewAsync(viewPath);
        }

        // Retrieve scoreboard
        var scoreboard = await scoreboardService.GetUserScoreboardAsync(currentUser.Id, currentUser.GroupId.Value, labId.Value, HttpContext.RequestAborted);
        if(scoreboard == null)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["RenderAsync:EmptyScoreboard", labId]);
            return await RenderViewAsync(viewPath);
        }

        ViewData["Scoreboard"] = scoreboard;

        return await RenderViewAsync(viewPath);
    }

    [HttpGet("")]
    public Task<IActionResult> RenderLabPageAsync(int? labId)
    {
        return RenderAsync(labId);
    }

    [HttpPost("flag")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitFlagAsync(int labId,
                                                     string code,
                                                     [FromServices] IFlagService flagService)
    {
        // Retrieve group ID
        var currentUser = await GetCurrentUserAsync();
        if(currentUser?.GroupId == null)
        {
            PostStatusMessage = new(StatusMessageType.Error, Localizer["SubmitFlagAsync:NoGroup"]);
            return RedirectToAction("RenderLabPage", new { labId });
        }

        try
        {
            // Try to submit flag
            bool success = await flagService.SubmitFlagAsync(currentUser.Id, labId, code, HttpContext.RequestAborted);
            if(success)
            {
                PostStatusMessage = new(StatusMessageType.Success, Localizer["SubmitFlagAsync:Success"]) { AutoHide = true };
            }
            else
            {
                PostStatusMessage = new(StatusMessageType.Error, Localizer["SubmitFlagAsync:Error"]);
            }
        }
        catch(Exception)
        {
            PostStatusMessage = new(StatusMessageType.Error, Localizer["SubmitFlagAsync:Error"]);
        }

        return RedirectToAction("RenderLabPage", new { labId });
    }

    [HttpGet("labserver")]
    public async Task<IActionResult> CallLabServerAsync(int labId,
                                                        [FromServices] ILabService labService,
                                                        [FromServices] ILabExecutionService labExecutionService)
    {
        // Retrieve group ID
        var currentUser = await GetCurrentUserAsync();
        if(currentUser?.GroupId == null)
        {
            PostStatusMessage = new(StatusMessageType.Error, Localizer["CallLabServerAsync:NoGroup"]);
            return RedirectToAction("RenderLabPage", new { labId });
        }

        // Retrieve lab data and check access
        var lab = await labService.FindLabByIdAsync(labId, HttpContext.RequestAborted);
        if(lab == null || (!lab.Visible && !currentUser.Privileges.HasAnyPrivilege(UserPrivileges.ViewAdminScoreboard | UserPrivileges.ViewLabs)))
        {
            PostStatusMessage = new(StatusMessageType.Error, Localizer["CallLabServerAsync:LabNotFound"]);
            return RedirectToAction("RenderLabPage", new { labId });
        }

        // Check whether lab is accessible by given group
        DateTime now = DateTime.Now;
        var labExecution = await labExecutionService.FindLabExecutionAsync(currentUser.GroupId.Value, labId, HttpContext.RequestAborted);
        if(labExecution == null || now < labExecution.Start)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["CallLabServerAsync:LabNotActive"]);
            return await RenderAsync(labId);
        }

        // Build authentication string
        var authData = new UserLoginRequest
        {
            UserId = currentUser.Id,
            UserDisplayName = currentUser.DisplayName,
            GroupId = currentUser.GroupId,
            GroupName = currentUser.Group?.DisplayName,
            AdminMode = false
        };
        string authString = new CryptoService(lab.ApiCode).Encrypt(authData.Serialize());

        // Build final URL
        string url = lab.ServerBaseUrl.TrimEnd().TrimEnd('/') + "/auth/login?code=" + authString;

        // Forward to server
        return Redirect(url);
    }
}
using System;
using System.Threading.Tasks;
using Ctf4e.Api.Models;
using Ctf4e.Api.Services;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Services;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Ctf4e.Server.Controllers;

[Route("dashboard")]
[Authorize(Policy = AuthenticationStrings.PolicyIsGroupMember)]
public class UserDashboardController : ControllerBase
{
    private readonly IStringLocalizer<UserDashboardController> _localizer;
    private readonly IScoreboardService _scoreboardService;
    private readonly ILabExecutionService _labExecutionService;
    private readonly IFlagService _flagService;
    private readonly ILabService _labService;

    public UserDashboardController(IUserService userService, IStringLocalizer<UserDashboardController> localizer, IScoreboardService scoreboardService, ILabExecutionService labExecutionService, IFlagService flagService, ILabService labService)
        : base("~/Views/UserDashboard.cshtml", userService)
    {
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        _scoreboardService = scoreboardService ?? throw new ArgumentNullException(nameof(scoreboardService));
        _labExecutionService = labExecutionService ?? throw new ArgumentNullException(nameof(labExecutionService));
        _flagService = flagService ?? throw new ArgumentNullException(nameof(flagService));
        _labService = labService ?? throw new ArgumentNullException(nameof(labService));
    }

    private async Task<IActionResult> RenderAsync(int? labId)
    {
        // Retrieve group ID
        var currentUser = await GetCurrentUserAsync();
        if(currentUser?.GroupId == null)
        {
            AddStatusMessage(_localizer["RenderAsync:NoGroup"], StatusMessageTypes.Error);
            return await RenderViewAsync();
        }

        // Show group's most recent lab
        if(labId == null)
        {
            var currentLabExecution = await _labExecutionService.GetMostRecentLabExecutionAsync(currentUser.GroupId.Value);
            if(currentLabExecution == null)
            {
                AddStatusMessage(_localizer["RenderAsync:NoActiveLab"], StatusMessageTypes.Info);
                return await RenderViewAsync();
            }

            labId = currentLabExecution.LabId;
        }

        // Retrieve scoreboard
        var scoreboard = await _scoreboardService.GetUserScoreboardAsync(currentUser.Id, currentUser.GroupId.Value, labId.Value, HttpContext.RequestAborted);
        if(scoreboard == null)
        {
            AddStatusMessage(_localizer["RenderAsync:EmptyScoreboard", labId], StatusMessageTypes.Error);
            return await RenderViewAsync();
        }

        ViewData["Scoreboard"] = scoreboard;

        return await RenderViewAsync(MenuItems.Group);
    }

    [HttpGet("")]
    public Task<IActionResult> RenderLabPageAsync(int? labId)
    {
        return RenderAsync(labId);
    }

    [HttpPost("flag")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitFlagAsync(int labId, string code)
    {
        // Retrieve group ID
        var currentUser = await GetCurrentUserAsync();
        if(currentUser?.GroupId == null)
        {
            AddStatusMessage(_localizer["SubmitFlagAsync:NoGroup"], StatusMessageTypes.Error);
            return await RenderLabPageAsync(labId);
        }

        // Try to submit flag
        bool success = await _flagService.SubmitFlagAsync(currentUser.Id, labId, code, HttpContext.RequestAborted);
        if(success)
        {
            AddStatusMessage(_localizer["SubmitFlagAsync:Success"], StatusMessageTypes.Success);
        }
        else
        {
            AddStatusMessage(_localizer["SubmitFlagAsync:Error"], StatusMessageTypes.Error);
        }

        return await RenderLabPageAsync(labId);
    }

    [HttpGet("labserver")]
    public async Task<IActionResult> CallLabServerAsync(int labId)
    {
        // Retrieve group ID
        var currentUser = await GetCurrentUserAsync();
        if(currentUser?.GroupId == null)
        {
            AddStatusMessage(_localizer["CallLabServerAsync:NoGroup"], StatusMessageTypes.Error);
            return await RenderViewAsync();
        }
            
        // Retrieve lab data
        var lab = await _labService.GetLabAsync(labId, HttpContext.RequestAborted);
        if(lab == null)
        {
            AddStatusMessage(_localizer["CallLabServerAsync:LabNotFound"], StatusMessageTypes.Error);
            return await RenderViewAsync();
        }

        // Check whether lab is accessible by given group
        DateTime now = DateTime.Now;
        var labExecution = await _labExecutionService.GetLabExecutionAsync(currentUser.GroupId.Value, labId, HttpContext.RequestAborted);
        if(labExecution == null || now < labExecution.PreStart)
        {
            AddStatusMessage(_localizer["CallLabServerAsync:LabNotActive"], StatusMessageTypes.Error);
            return await RenderViewAsync();
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
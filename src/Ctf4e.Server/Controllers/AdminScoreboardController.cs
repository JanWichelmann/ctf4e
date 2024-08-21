using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Ctf4e.Api.Models;
using Ctf4e.Api.Services;
using Ctf4e.Server.Authorization;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Models;
using Ctf4e.Server.Services;
using Ctf4e.Server.Services.Sync;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Ctf4e.Server.Controllers;

[Route("admin/scoreboard")]
[AnyUserPrivilege(UserPrivileges.ViewAdminScoreboard)]
public class AdminScoreboardController(IUserService userService, IScoreboardService scoreboardService)
    : ControllerBase<AdminScoreboardController>(userService)
{
    protected override MenuItems ActiveMenuItem => MenuItems.AdminScoreboard;

    private readonly IUserService _userService = userService;

    private async Task<IActionResult> RenderAsync(string viewPath, int labId, int slotId, bool groupMode, bool includeTutors)
    {
        var scoreboard = await scoreboardService.GetAdminScoreboardAsync(labId, slotId, groupMode, includeTutors, HttpContext.RequestAborted);

        return await RenderViewAsync(viewPath, scoreboard);
    }

    [HttpGet]
    public async Task<IActionResult> RenderScoreboardAsync([FromServices] ILabExecutionService labExecutionService, int? labId, int? slotId, bool groupMode, bool includeTutors)
    {
        if(labId == null || slotId == null)
        {
            // Show the most recently executed lab and slot as default
            var recentLabExecution = await labExecutionService.FindMostRecentLabExecutionAsync(HttpContext.RequestAborted);
            if(recentLabExecution != null)
            {
                labId = recentLabExecution.LabId;
                slotId = recentLabExecution.Group?.SlotId;
            }
        }

        return await RenderAsync("~/Views/AdminScoreboard.cshtml", labId ?? 0, slotId ?? 0, groupMode, includeTutors);
    }

    [HttpPost("exercisesubmission/delete")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditAdminScoreboard)]
    public async Task<IActionResult> DeleteExerciseSubmissionAsync([FromServices] IExerciseService exerciseService, int labId, int slotId, bool groupMode, bool includeTutors, int submissionId)
    {
        try
        {
            // Delete submission
            await exerciseService.DeleteExerciseSubmissionAsync(submissionId, HttpContext.RequestAborted);

            AddStatusMessage(StatusMessageType.Success, Localizer["DeleteExerciseSubmissionAsync:Success"]);
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Delete exercise submission");
            AddStatusMessage(StatusMessageType.Error, Localizer["DeleteExerciseSubmissionAsync:UnknownError"]);
        }

        return await RenderAsync("~/Views/AdminScoreboard.cshtml", labId, slotId, groupMode, includeTutors);
    }

    [HttpPost("exercisesubmission/deletemultiple")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditAdminScoreboard)]
    public async Task<IActionResult> DeleteExerciseSubmissionsAsync([FromServices] IExerciseService exerciseService, int labId, int slotId, bool groupMode, bool includeTutors, List<int> submissionIds)
    {
        try
        {
            // Delete submissions
            await exerciseService.DeleteExerciseSubmissionsAsync(submissionIds, HttpContext.RequestAborted);

            AddStatusMessage(StatusMessageType.Success, Localizer["DeleteExerciseSubmissionsAsync:Success"]);
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Delete exercise submissions");
            AddStatusMessage(StatusMessageType.Error, Localizer["DeleteExerciseSubmissionsAsync:UnknownError"]);
        }

        return await RenderAsync("~/Views/AdminScoreboard.cshtml", labId, slotId, groupMode, includeTutors);
    }

    [HttpPost("exercisesubmission/create")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditAdminScoreboard)]
    public async Task<IActionResult> CreateExerciseSubmissionAsync([FromServices] IExerciseService exerciseService, int labId, int slotId, bool groupMode, bool includeTutors, int exerciseId, int userId, DateTime submissionTime, bool passed, int weight)
    {
        try
        {
            // Create submission
            var submission = new ExerciseSubmission
            {
                ExerciseId = exerciseId,
                UserId = userId,
                ExercisePassed = passed,
                SubmissionTime = submissionTime,
                Weight = passed ? 1 : weight
            };
            await exerciseService.CreateExerciseSubmissionAsync(submission, HttpContext.RequestAborted);

            AddStatusMessage(StatusMessageType.Success, Localizer["CreateExerciseSubmissionAsync:Success"]);
        }
        catch(InvalidOperationException ex)
        {
            GetLogger().LogError(ex, "Create exercise submission");
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateExerciseSubmissionAsync:UnknownError"]);
        }

        return await RenderAsync("~/Views/AdminScoreboard.cshtml", labId, slotId, groupMode, includeTutors);
    }

    [HttpPost("flagsubmission/delete")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditAdminScoreboard)]
    public async Task<IActionResult> DeleteFlagSubmissionAsync([FromServices] IFlagService flagService, int labId, int slotId, bool groupMode, bool includeTutors, int userId, int flagId)
    {
        try
        {
            // Delete submission
            await flagService.DeleteFlagSubmissionAsync(userId, flagId, HttpContext.RequestAborted);

            AddStatusMessage(StatusMessageType.Success, Localizer["DeleteFlagSubmissionAsync:Success"]);
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Delete flag submission");
            AddStatusMessage(StatusMessageType.Error, Localizer["DeleteFlagSubmissionAsync:UnknownError"]);
        }

        return await RenderAsync("~/Views/AdminScoreboard.cshtml", labId, slotId, groupMode, includeTutors);
    }

    [HttpPost("flagsubmission/create")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditAdminScoreboard)]
    public async Task<IActionResult> CreateFlagSubmissionAsync([FromServices] IFlagService flagService, int labId, int slotId, bool groupMode, bool includeTutors, int userId, int flagId, DateTime submissionTime)
    {
        try
        {
            // Create submission
            var submission = new FlagSubmission
            {
                UserId = userId,
                FlagId = flagId,
                SubmissionTime = submissionTime
            };
            await flagService.CreateFlagSubmissionAsync(submission, HttpContext.RequestAborted);

            AddStatusMessage(StatusMessageType.Success, Localizer["CreateFlagSubmissionAsync:Success"]);
        }
        catch(InvalidOperationException ex)
        {
            GetLogger().LogError(ex, "Create flag submission");
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateFlagSubmissionAsync:UnknownError"]);
        }

        return await RenderAsync("~/Views/AdminScoreboard.cshtml", labId, slotId, groupMode, includeTutors);
    }

    [HttpGet("labserver")]
    [AnyUserPrivilege(UserPrivileges.LoginAsLabServerAdmin)]
    public async Task<IActionResult> CallLabServerAsync([FromServices] ILabService labService, [FromServices] IGroupService groupService, int labId, int userId)
    {
        // Retrieve lab data
        var lab = await labService.FindLabByIdAsync(labId, HttpContext.RequestAborted);
        if(lab == null)
        {
            PostStatusMessage = new(StatusMessageType.Error, Localizer["CallLabServerAsync:NotFound"]);
            return RedirectToAction("RenderScoreboard");
        }

        // Build authentication string
        var user = await _userService.FindUserByIdAsync(userId, HttpContext.RequestAborted);
        var group = user.GroupId == null ? null : await groupService.FindGroupByIdAsync(user.GroupId ?? -1, HttpContext.RequestAborted);
        var authData = new UserLoginRequest
        {
            UserId = userId,
            UserDisplayName = user.DisplayName,
            GroupId = group?.Id,
            GroupName = group?.DisplayName,
            AdminMode = true
        };
        string authString = new CryptoService(lab.ApiCode).Encrypt(authData.Serialize());

        // Build final URL
        string url = lab.ServerBaseUrl.TrimEnd().TrimEnd('/') + "/auth/login?code=" + authString;

        // Forward to server
        return Redirect(url);
    }

    [HttpPost("sync/moodle")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.TransferResults)]
    public async Task<IActionResult> UploadToMoodleAsync([FromServices] IMoodleService moodleService)
    {
        try
        {
            await moodleService.UploadStateToMoodleAsync(HttpContext.RequestAborted);

            AddStatusMessage(StatusMessageType.Success, Localizer["UploadToMoodleAsync:Success"]);
        }
        catch(InvalidOperationException ex)
        {
            GetLogger().LogError(ex, "Upload to Moodle");
            AddStatusMessage(StatusMessageType.Error, Localizer["UploadToMoodleAsync:UnknownError"]);
        }

        return await RenderAsync("~/Views/AdminScoreboard.cshtml", 0, 0, false, false);
    }

    [HttpGet("sync/csv")]
    [AnyUserPrivilege(UserPrivileges.TransferResults)]
    public async Task<IActionResult> DownloadAsCsvAsync([FromServices] ICsvService csvService)
    {
        try
        {
            string csv = await csvService.GetLabStatesAsync(HttpContext.RequestAborted);
            return File(Encoding.UTF8.GetBytes(csv), "text/csv", "labstates.csv");
        }
        catch(InvalidOperationException ex)
        {
            GetLogger().LogError(ex, "Download as CSV");
            AddStatusMessage(StatusMessageType.Error, Localizer["DownloadAsCsvAsync:UnknownError"]);
        }

        return await RenderAsync("~/Views/AdminScoreboard.cshtml", 0, 0, false, false);
    }
}
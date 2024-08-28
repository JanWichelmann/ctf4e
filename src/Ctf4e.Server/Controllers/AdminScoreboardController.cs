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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ctf4e.Server.Controllers;

[Route("admin/scoreboard")]
[AnyUserPrivilege(UserPrivileges.ViewAdminScoreboard)]
public class AdminScoreboardController(IUserService userService, IScoreboardService scoreboardService, IAdminScoreboardService adminScoreboardService)
    : ControllerBase<AdminScoreboardController>(userService)
{
    protected override MenuItems ActiveMenuItem => MenuItems.AdminScoreboard;

    private readonly IUserService _userService = userService;

    private async Task<IActionResult> RenderAsync(string viewPath, object model)
    {
        var labService = HttpContext.RequestServices.GetRequiredService<ILabService>();
        var slotService = HttpContext.RequestServices.GetRequiredService<ISlotService>();

        ViewData["Labs"] = await labService.GetSelectLabListAsync(HttpContext.RequestAborted);
        ViewData["Slots"] = await slotService.GetSelectSlotListAsync(HttpContext.RequestAborted);

        return await RenderViewAsync(viewPath, model);
    }

    private async Task<(int LabId, int SlotId)> GetRecentLabAndSlotAsync()
    {
        var labExecutionService = HttpContext.RequestServices.GetRequiredService<ILabExecutionService>();

        // Show the most recently executed lab and slot as default
        var labExecution = await labExecutionService.FindMostRecentLabExecutionAsync(HttpContext.RequestAborted);
        if(labExecution != null)
            return (labExecution.LabId, labExecution.Group?.SlotId ?? 0);
        return (0, 0);
    }

    [HttpGet]
    [HttpGet("stats")]
    public async Task<IActionResult> ShowStatisticsDashboardAsync(int? labId, int? slotId)
    {
        if(labId == null || slotId == null)
            (labId, slotId) = await GetRecentLabAndSlotAsync();

        var statistics = await adminScoreboardService.GetStatisticsAsync(labId.Value, slotId.Value, HttpContext.RequestAborted);

        return await RenderAsync("~/Views/Admin/Scoreboard/Index.cshtml", statistics);
    }

    [HttpGet("groups")]
    public async Task<IActionResult> ShowGroupsOverviewAsync(int? labId, int? slotId)
    {
        if(labId == null || slotId == null)
            (labId, slotId) = await GetRecentLabAndSlotAsync();
        
        var overview = await adminScoreboardService.GetOverviewAsync(labId.Value, slotId.Value, HttpContext.RequestAborted);
        
        return await RenderAsync("~/Views/Admin/Scoreboard/Groups.cshtml", overview);
    }
    
    [HttpGet("users")]
    public async Task<IActionResult> ShowUsersOverviewAsync(int? labId, int? slotId)
    {
        if(labId == null || slotId == null)
            (labId, slotId) = await GetRecentLabAndSlotAsync();
        
        var overview = await adminScoreboardService.GetOverviewAsync(labId.Value, slotId.Value, HttpContext.RequestAborted);
        
        return await RenderAsync("~/Views/Admin/Scoreboard/Users.cshtml", overview);
    }

    [HttpGet("group/{groupId}")]
    public async Task<IActionResult> ShowGroupDashboardAsync(int groupId, int labId)
    {
        return await RenderAsync("~/Views/Admin/Scoreboard/GroupDashboard.cshtml", null);
    }
    
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> ShowUserDashboardAsync(int userId, int labId)
    {
        return await RenderAsync("~/Views/Admin/Scoreboard/UserDashboard.cshtml", null);
    }
    
    [HttpGet("export")]
    public async Task<IActionResult> ShowExportPageAsync()
    {
        return await RenderAsync("~/Views/Admin/Scoreboard/Export.cshtml", null);
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

        return await RenderAsync("~/Views/AdminScoreboard.cshtml", null);
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

        return await RenderAsync("~/Views/AdminScoreboard.cshtml", null);
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

        return await RenderAsync("~/Views/AdminScoreboard.cshtml", null);
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

        return await RenderAsync("~/Views/AdminScoreboard.cshtml", null);
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

        return await RenderAsync("~/Views/AdminScoreboard.cshtml", null);
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
        // TODO move this and next function to own sub view
        
        try
        {
            await moodleService.UploadStateToMoodleAsync(HttpContext.RequestAborted);

            PostStatusMessage = new StatusMessage(StatusMessageType.Success, Localizer["UploadToMoodleAsync:Success"]);
        }
        catch(InvalidOperationException ex)
        {
            GetLogger().LogError(ex, "Upload to Moodle");
            PostStatusMessage = new StatusMessage(StatusMessageType.Error, Localizer["UploadToMoodleAsync:UnknownError"]);
        }

        return RedirectToAction("ShowStatisticsDashboard");
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

        return await ShowStatisticsDashboardAsync(null, null);
    }
}
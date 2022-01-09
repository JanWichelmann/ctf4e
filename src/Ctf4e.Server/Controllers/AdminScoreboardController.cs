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
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Ctf4e.Server.Controllers;

[Route("admin/scoreboard")]
[AnyUserPrivilege(UserPrivileges.ViewAdminScoreboard)]
public class AdminScoreboardController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IStringLocalizer<AdminScoreboardController> _localizer;
    private readonly ILogger<AdminScoreboardController> _logger;
    private readonly IScoreboardService _scoreboardService;

    public AdminScoreboardController(IUserService userService, IStringLocalizer<AdminScoreboardController> localizer, ILogger<AdminScoreboardController> logger, IScoreboardService scoreboardService)
        : base("~/Views/AdminScoreboard.cshtml", userService)
    {
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _scoreboardService = scoreboardService ?? throw new ArgumentNullException(nameof(scoreboardService));
    }

    private async Task<IActionResult> RenderAsync(int labId, int slotId, bool groupMode, bool includeTutors)
    {
        var scoreboard = await _scoreboardService.GetAdminScoreboardAsync(labId, slotId, groupMode, includeTutors, HttpContext.RequestAborted);

        return await RenderViewAsync(MenuItems.AdminScoreboard, scoreboard);
    }

    [HttpGet]
    public async Task<IActionResult> RenderScoreboardAsync([FromServices] ILabExecutionService labExecutionService, int? labId, int? slotId, bool groupMode, bool includeTutors)
    {
        if(labId == null || slotId == null)
        {
            // Show the most recently executed lab and slot as default
            var recentLabExecution = await labExecutionService.GetMostRecentLabExecutionAsync(HttpContext.RequestAborted);
            if(recentLabExecution != null)
            {
                labId = recentLabExecution.LabId;
                slotId = recentLabExecution.Group?.SlotId;
            }
        }

        return await RenderAsync(labId ?? 0, slotId ?? 0, groupMode, includeTutors);
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

            AddStatusMessage(_localizer["DeleteExerciseSubmissionAsync:Success"], StatusMessageTypes.Success);
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Delete exercise submission");
            AddStatusMessage(_localizer["DeleteExerciseSubmissionAsync:UnknownError"], StatusMessageTypes.Error);
        }

        return await RenderAsync(labId, slotId, groupMode, includeTutors);
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

            AddStatusMessage(_localizer["DeleteExerciseSubmissionsAsync:Success"], StatusMessageTypes.Success);
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Delete exercise submissions");
            AddStatusMessage(_localizer["DeleteExerciseSubmissionsAsync:UnknownError"], StatusMessageTypes.Error);
        }

        return await RenderAsync(labId, slotId, groupMode, includeTutors);
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

            AddStatusMessage(_localizer["CreateExerciseSubmissionAsync:Success"], StatusMessageTypes.Success);
        }
        catch(InvalidOperationException ex)
        {
            _logger.LogError(ex, "Create exercise submission");
            AddStatusMessage(_localizer["CreateExerciseSubmissionAsync:UnknownError"], StatusMessageTypes.Error);
        }

        return await RenderAsync(labId, slotId, groupMode, includeTutors);
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

            AddStatusMessage(_localizer["DeleteFlagSubmissionAsync:Success"], StatusMessageTypes.Success);
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Delete flag submission");
            AddStatusMessage(_localizer["DeleteFlagSubmissionAsync:UnknownError"], StatusMessageTypes.Error);
        }

        return await RenderAsync(labId, slotId, groupMode, includeTutors);
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

            AddStatusMessage(_localizer["CreateFlagSubmissionAsync:Success"], StatusMessageTypes.Success);
        }
        catch(InvalidOperationException ex)
        {
            _logger.LogError(ex, "Create flag submission");
            AddStatusMessage(_localizer["CreateFlagSubmissionAsync:UnknownError"], StatusMessageTypes.Error);
        }

        return await RenderAsync(labId, slotId, groupMode, includeTutors);
    }

    [HttpGet("labserver")]
    [AnyUserPrivilege(UserPrivileges.LoginAsLabServerAdmin)]
    public async Task<IActionResult> CallLabServerAsync([FromServices] ILabService labService, int labId, int userId)
    {
        // Retrieve lab data
        var lab = await labService.GetLabAsync(labId, HttpContext.RequestAborted);
        if(lab == null)
        {
            AddStatusMessage(_localizer["CallLabServerAsync:NotFound"], StatusMessageTypes.Error);
            return await RenderViewAsync();
        }

        // Build authentication string
        var user = await _userService.FindUserByIdAsync(userId, HttpContext.RequestAborted);
        var group = user.GroupId == null ? null : await _userService.GetGroupAsync(user.GroupId ?? -1, HttpContext.RequestAborted);
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

            AddStatusMessage(_localizer["UploadToMoodleAsync:Success"], StatusMessageTypes.Success);
        }
        catch(InvalidOperationException ex)
        {
            _logger.LogError(ex, "Upload to Moodle");
            AddStatusMessage(_localizer["UploadToMoodleAsync:UnknownError"], StatusMessageTypes.Error);
        }

        return await RenderAsync(0, 0, false, false);
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
            _logger.LogError(ex, "Download as CSV");
            AddStatusMessage(_localizer["DownloadAsCsvAsync:UnknownError"], StatusMessageTypes.Error);
        }

        return await RenderAsync(0, 0, false, false);
    }
}
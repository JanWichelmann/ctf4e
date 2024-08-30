using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ctf4e.Server.Authorization;
using Ctf4e.Server.InputModels;
using Ctf4e.Server.Models;
using Ctf4e.Server.Services;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Ctf4e.Server.Controllers;

public partial class AdminScoreboardController
{
    [HttpPost("exercise-submission/delete")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditAdminScoreboard)]
    public async Task<IActionResult> DeleteExerciseSubmissionAsync(int id, [FromServices] IExerciseService exerciseService)
    {
        try
        {
            // Delete submission
            await exerciseService.DeleteExerciseSubmissionAsync(id, HttpContext.RequestAborted);

            return Ok();
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Delete exercise submission");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost("exercise-submission/delete-many")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditAdminScoreboard)]
    public async Task<IActionResult> DeleteExerciseSubmissionsAsync([FromBody] List<int> ids, [FromServices] IExerciseService exerciseService)
    {
        if(ids == null || ids.Count == 0)
            return BadRequest();

        try
        {
            // Delete submissions
            await exerciseService.DeleteExerciseSubmissionsAsync(ids, HttpContext.RequestAborted);

            return Ok();
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Delete exercise submissions");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost("exercise-submission/create")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditAdminScoreboard)]
    public async Task<IActionResult> CreateExerciseSubmissionAsync([FromBody] AdminExerciseSubmissionInputModel input, [FromServices] IExerciseService exerciseService)
    {
        if(!ModelState.IsValid)
            return BadRequest(ModelState);

        if(input.UserId == null && input.GroupId == null)
            return BadRequest(new { error = "Either UserId or GroupId must be set." });

        var submissionTime = input.SubmissionTime ?? DateTime.Now;
        
        try
        {
            // If a group was given instead of a specific user, create submissions for all group members
            List<int> users;
            if(input.UserId != null)
                users = [input.UserId.Value];
            else
            {
                var groupMembers = await userService.GetGroupMembersAsync(input.GroupId!.Value, HttpContext.RequestAborted);
                users = groupMembers.Select(u => u.Id).ToList();
            }

            foreach(var userId in users)
            {
                // Create submission
                var submission = new ExerciseSubmission
                {
                    ExerciseId = input.ExerciseId,
                    UserId = userId,
                    ExercisePassed = input.ExercisePassed,
                    SubmissionTime = submissionTime,
                    Weight = input.ExercisePassed ? 1 : input.Weight
                };
                await exerciseService.CreateExerciseSubmissionAsync(submission, HttpContext.RequestAborted);
            }

            AddStatusMessage(StatusMessageType.Success, Localizer["CreateExerciseSubmissionAsync:Success"]);
        }
        catch(InvalidOperationException ex)
        {
            GetLogger().LogError(ex, "Create exercise submission");
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateExerciseSubmissionAsync:UnknownError"]);
        }

        return await RenderAsync("~/Views/AdminScoreboard.cshtml", null);
    }

    [HttpPost("flag-submission/delete")]
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

    [HttpPost("flag-submission/create")]
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
}
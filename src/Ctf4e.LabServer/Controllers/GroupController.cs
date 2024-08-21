using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ctf4e.Api.Exceptions;
using Ctf4e.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ctf4e.LabServer.Constants;
using Ctf4e.LabServer.InputModels;
using Ctf4e.LabServer.Services;
using Ctf4e.Utilities;
using Microsoft.Extensions.Logging;

namespace Ctf4e.LabServer.Controllers;

[Route("")]
[Route("group")]
[Authorize]
public class GroupController(IStateService stateService, ICtfApiClient ctfApiClient, ILabConfigurationService labConfiguration)
    : ControllerBase<GroupController>
{
    protected override MenuItems ActiveMenuItem => MenuItems.Group;

    private async Task<IActionResult> RenderAsync()
    {
        // Retrieve user ID
        int userId = GetCurrentUser().UserId;

        // Pass user scoreboard
        ViewData["Scoreboard"] = await stateService.GetUserScoreboardAsync(userId);

        return RenderView("~/Views/Group.cshtml");
    }

    [HttpGet("")]
    public Task<IActionResult> RenderPageAsync()
    {
        return RenderAsync();
    }

    private async Task<bool> CheckInputAsync(int exerciseId, object input)
    {
        // Don't allow the user to cancel this too early, but also ensure that the application doesn't block too long
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Get current user
        int userId = GetCurrentUser().UserId;

        // Check input
        bool correct = await stateService.CheckInputAsync(exerciseId, userId, input, cts.Token);

        // Notify CTF system
        int? exerciseNumber = labConfiguration.CurrentConfiguration.Exercises.FirstOrDefault(e => e.Id == exerciseId)?.CtfExerciseNumber;
        if(exerciseNumber != null)
        {
            await ctfApiClient.CreateExerciseSubmissionAsync(new Ctf4e.Api.Models.ApiExerciseSubmission
            {
                ExerciseNumber = exerciseNumber.Value,
                UserId = userId,
                ExercisePassed = correct,
                Weight = 1,
                SubmissionTime = DateTime.Now
            }, cts.Token);
        }

        return correct;
    }

    [HttpPost("check/string")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckStringInputAsync(StringExerciseInputData inputData)
    {
        try
        {
            ViewData["LastStringInput"] = inputData;

            if(!ModelState.IsValid)
            {
                AddStatusMessage(StatusMessageType.Error, Localizer["CheckStringInputAsync:InvalidInput"]);
                return await RenderAsync();
            }

            // Check input
            if(!await CheckInputAsync(inputData.ExerciseId, inputData.Input))
            {
                AddStatusMessage(StatusMessageType.Error, Localizer["CheckStringInputAsync:Wrong"]);
                return await RenderAsync();
            }

            AddStatusMessage(StatusMessageType.Success, Localizer["CheckStringInputAsync:Success"]);
            return await RenderAsync();
        }
        catch(CtfApiException ex)
        {
            GetLogger().LogError(ex, "CTF error");

            if(GetAdminMode())
            {
                AddStatusMessage(StatusMessageType.Error, Localizer["CtfError:Admin"]);
                StatusMessages.Add(new(StatusMessageType.Info, ex.FormattedResponseContent ?? "(none)") { Preformatted = true });
            }
            else
                AddStatusMessage(StatusMessageType.Error, Localizer["CtfError:Default"]);

            return await RenderAsync();
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Check string input");
            AddStatusMessage(StatusMessageType.Error, Localizer["CheckStringInputAsync:UnknownError"]);

            // Show more details for admins, so they can debug the issue
            if(GetAdminMode())
                AddStatusMessage(StatusMessageType.Info, Localizer["ExceptionMessage", ex.Message]);

            return await RenderAsync();
        }
    }

    [HttpPost("check/multiplechoice")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckMultipleChoiceInputAsync(MultipleChoiceExerciseInputData inputData)
    {
        try
        {
            ViewData["LastMultipleChoiceInput"] = inputData;

            if(!ModelState.IsValid)
            {
                AddStatusMessage(StatusMessageType.Error, Localizer["CheckMultipleChoiceInputAsync:InvalidInput"]);
                return await RenderAsync();
            }

            // Check input
            if(!await CheckInputAsync(inputData.ExerciseId, inputData.SelectedOptions))
            {
                AddStatusMessage(StatusMessageType.Error, Localizer["CheckMultipleChoiceInputAsync:Wrong"]);
                return await RenderAsync();
            }

            AddStatusMessage(StatusMessageType.Success, Localizer["CheckMultipleChoiceInputAsync:Success"]);
            return await RenderAsync();
        }
        catch(CtfApiException ex)
        {
            GetLogger().LogError(ex, "CTF error");

            if(GetAdminMode())
            {
                AddStatusMessage(StatusMessageType.Error, Localizer["CtfError:Admin"]);
                StatusMessages.Add(new(StatusMessageType.Info, ex.FormattedResponseContent ?? "(none)") { Preformatted = true });
            }
            else
                AddStatusMessage(StatusMessageType.Error, Localizer["CtfError:Default"]);

            return await RenderAsync();
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Check multiple choice input");
            AddStatusMessage(StatusMessageType.Error, Localizer["CheckMultipleChoiceInputAsync:UnknownError"]);

            // Show more details for admins, so they can debug the issue
            if(GetAdminMode())
                AddStatusMessage(StatusMessageType.Info, Localizer["ExceptionMessage", ex.Message]);

            return await RenderAsync();
        }
    }

    [HttpPost("check/script")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckScriptInputAsync(ScriptExerciseInputData inputData)
    {
        try
        {
            ViewData["LastScriptInput"] = inputData;

            if(!ModelState.IsValid)
            {
                AddStatusMessage(StatusMessageType.Error, Localizer["CheckScriptInputAsync:InvalidInput"]);
                return await RenderAsync();
            }

            // Check input
            if(!await CheckInputAsync(inputData.ExerciseId, inputData.Input))
            {
                AddStatusMessage(StatusMessageType.Error, Localizer["CheckScriptInputAsync:Wrong"]);
                return await RenderAsync();
            }

            AddStatusMessage(StatusMessageType.Success, Localizer["CheckScriptInputAsync:Success"]);
            return await RenderAsync();
        }
        catch(CtfApiException ex)
        {
            GetLogger().LogError(ex, "CTF error");

            if(GetAdminMode())
            {
                AddStatusMessage(StatusMessageType.Error, Localizer["CtfError:Admin"]);
                StatusMessages.Add(new(StatusMessageType.Info, ex.FormattedResponseContent ?? "(none)") { Preformatted = true });
            }
            else
                AddStatusMessage(StatusMessageType.Error, Localizer["CtfError:Default"]);

            return await RenderAsync();
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Check script input");
            AddStatusMessage(StatusMessageType.Error, Localizer["CheckScriptInputAsync:UnknownError"]);

            // Show more details for admins, so they can debug the issue
            if(GetAdminMode())
                AddStatusMessage(StatusMessageType.Info, Localizer["ExceptionMessage", ex.Message]);

            return await RenderAsync();
        }
    }

    [HttpPost("admin/solve")]
    [Authorize(Policy = AuthenticationStrings.PolicyAdminMode)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkExerciseAsSolvedAsync(int exerciseId)
    {
        try
        {
            // Get current user
            int userId = GetCurrentUser().UserId;

            // Set status
            await stateService.MarkExerciseSolvedAsync(exerciseId, userId);

            // Notify CTF system
            int? exerciseNumber = labConfiguration.CurrentConfiguration.Exercises.FirstOrDefault(e => e.Id == exerciseId)?.CtfExerciseNumber;
            if(exerciseNumber != null)
            {
                await ctfApiClient.CreateExerciseSubmissionAsync(new Ctf4e.Api.Models.ApiExerciseSubmission
                {
                    ExerciseNumber = exerciseNumber.Value,
                    UserId = userId,
                    ExercisePassed = true,
                    Weight = 1,
                    SubmissionTime = DateTime.Now
                }, CancellationToken.None);
            }

            AddStatusMessage(StatusMessageType.Success, Localizer["MarkExerciseAsSolvedAsync:Success"]);
            return await RenderAsync();
        }
        catch(ArgumentException)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["MarkExerciseAsSolvedAsync:NotFound"]);
            return await RenderAsync();
        }
        catch(CtfApiException ex)
        {
            GetLogger().LogError(ex, "CTF error");

            if(GetAdminMode())
            {
                AddStatusMessage(StatusMessageType.Error, Localizer["CtfError:Admin"]);
                StatusMessages.Add(new(StatusMessageType.Info, ex.FormattedResponseContent ?? "(none)") { Preformatted = true });
            }
            else
                AddStatusMessage(StatusMessageType.Error, Localizer["CtfError:Default"]);

            return await RenderAsync();
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Mark exercise as solved");
            AddStatusMessage(StatusMessageType.Error, Localizer["MarkExerciseAsSolvedAsync:UnknownError"]);

            // Show more details for admins, so they can debug the issue
            if(GetAdminMode())
                AddStatusMessage(StatusMessageType.Info, Localizer["ExceptionMessage", ex.Message]);

            return await RenderAsync();
        }
    }

    [HttpPost("admin/reset")]
    [Authorize(Policy = AuthenticationStrings.PolicyAdminMode)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetExerciseStatusAsync(int exerciseId)
    {
        try
        {
            // Get current user
            int userId = GetCurrentUser().UserId;

            // Reset status
            await stateService.ResetExerciseStatusAsync(exerciseId, userId);

            AddStatusMessage(StatusMessageType.Success, Localizer["ResetExerciseStatusAsync:Success"]);
            return await RenderAsync();
        }
        catch(ArgumentException)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["ResetExerciseStatusAsync:NotFound"]);
            return await RenderAsync();
        }
        catch(CtfApiException ex)
        {
            GetLogger().LogError(ex, "CTF error");

            if(GetAdminMode())
            {
                AddStatusMessage(StatusMessageType.Error, Localizer["CtfError:Admin"]);
                StatusMessages.Add(new(StatusMessageType.Info, ex.FormattedResponseContent ?? "(none)") { Preformatted = true });
            }
            else
                AddStatusMessage(StatusMessageType.Error, Localizer["CtfError:Default"]);

            return await RenderAsync();
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Reset exercise");
            AddStatusMessage(StatusMessageType.Error, Localizer["ResetExerciseStatusAsync:UnknownError"]);

            // Show more details for admins, so they can debug the issue
            if(GetAdminMode())
                AddStatusMessage(StatusMessageType.Info, Localizer["ExceptionMessage", ex.Message]);

            return await RenderAsync();
        }
    }
}
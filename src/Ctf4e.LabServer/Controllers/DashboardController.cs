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
using Ctf4e.LabServer.Options;
using Ctf4e.LabServer.Services;
using Ctf4e.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ctf4e.LabServer.Controllers;

[Route("")]
[Route("group")]
[Authorize]
public class DashboardController(IStateService stateService, ICtfApiClient ctfApiClient, ILabConfigurationService labConfiguration, IOptions<LabOptions> options)
    : ControllerBase<DashboardController>
{
    protected override MenuItems ActiveMenuItem => MenuItems.Group;

    private ExerciseInput _lastExerciseInput;

    /// <summary>
    /// Stores the last exercise input in the controller's TempData collection, so it is available on the next page render.
    ///
    /// We do aggressive POST-Redirect-GET in this class, to prevent accidental resubmissions of wrong solutions due to refreshing the page.
    /// </summary>
    private ExerciseInput LastExerciseInput
    {
        // The value can be consumed exactly once; we cache it in a variable so it can be accessed multiple times
        get => _lastExerciseInput ??= TempData.GetJson<ExerciseInput>("LastExerciseInput");
        set
        {
            if(value == null)
            {
                if(TempData.ContainsKey("LastExerciseInput"))
                    TempData.Remove("LastExerciseInput");
            }
            else
                TempData.SetJson("LastExerciseInput", value);
                    
            // Usually this _should_ not be accessed, as we redirect after completing the requested action.
            // A notable exception is admin mode, where we sometimes directly render the view. Set this variable
            // so we also have the last input in that case.
            _lastExerciseInput = value;
        }
    }

    [HttpGet]
    public async Task<IActionResult> ShowDashboardAsync()
    {
        // Retrieve user ID
        int userId = GetCurrentUser().UserId;

        // Pass user scoreboard
        ViewData["Scoreboard"] = await stateService.GetUserScoreboardAsync(userId, HttpContext.RequestAborted);

        ViewData["LastExerciseInput"] = LastExerciseInput;

        return RenderView("~/Views/Dashboard/Index.cshtml");
    }

    private async Task<bool> CheckInputAsync(int exerciseId, object input)
    {
        // Don't allow the user to cancel this too early, but also ensure that the application doesn't block too long
        // TODO we may want to implement a JS-based grading button that indicates that the grading process is running and does not leave the site in a loading state forever
        int timeout = options.Value.DockerContainerGradingTimeout ?? 30;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));

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
    public async Task<IActionResult> CheckStringInputAsync(StringExerciseInput exerciseInput)
    {
        try
        {
            LastExerciseInput = exerciseInput;

            if(!ModelState.IsValid)
            {
                PostStatusMessage = new(StatusMessageType.Error, Localizer["CheckStringInputAsync:InvalidInput"]);
                return RedirectToAction("ShowDashboard");
            }

            // Check input
            if(!await CheckInputAsync(exerciseInput.ExerciseId, exerciseInput.Input))
            {
                PostStatusMessage = new(StatusMessageType.Error, Localizer["CheckStringInputAsync:Wrong"]);
                return RedirectToAction("ShowDashboard");
            }

            PostStatusMessage = new(StatusMessageType.Success, Localizer["CheckStringInputAsync:Success"]);
            return RedirectToAction("ShowDashboard");
        }
        catch(CtfApiException ex)
        {
            GetLogger().LogError(ex, "CTF error");

            if(GetAdminMode())
            {
                AddStatusMessage(StatusMessageType.Error, Localizer["CtfError:Admin"]);
                StatusMessages.Add(new(StatusMessageType.Info, ex.FormattedResponseContent ?? "(none)") { Preformatted = true });
                return await ShowDashboardAsync();
            }
            else
            {
                PostStatusMessage = new(StatusMessageType.Error, Localizer["CtfError:Default"]);
                return RedirectToAction("ShowDashboard");
            }
        }
        catch(OperationCanceledException ex)
        {
            GetLogger().LogError(ex, "Check string input: Timeout");
            PostStatusMessage = new(StatusMessageType.Error, Localizer["CheckStringInputAsync:Timeout"]);

            return RedirectToAction("ShowDashboard");
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Check string input");

            // Show more details for admins, so they can debug the issue
            if(GetAdminMode())
            {
                AddStatusMessage(StatusMessageType.Error, Localizer["CheckStringInputAsync:UnknownError"]);
                AddStatusMessage(StatusMessageType.Info, Localizer["ExceptionMessage", ex.Message]);
                return await ShowDashboardAsync();
            }

            PostStatusMessage = new(StatusMessageType.Error, Localizer["CheckStringInputAsync:UnknownError"]);
            return RedirectToAction("ShowDashboard");
        }
    }

    [HttpPost("check/multiplechoice")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckMultipleChoiceInputAsync(MultipleChoiceExerciseInput exerciseInput)
    {
        try
        {
            LastExerciseInput = exerciseInput;

            if(!ModelState.IsValid)
            {
                PostStatusMessage = new(StatusMessageType.Error, Localizer["CheckMultipleChoiceInputAsync:InvalidInput"]);
                return RedirectToAction("ShowDashboard");
            }

            // Check input
            if(!await CheckInputAsync(exerciseInput.ExerciseId, exerciseInput.SelectedOptions))
            {
                PostStatusMessage = new(StatusMessageType.Error, Localizer["CheckMultipleChoiceInputAsync:Wrong"]);
                return RedirectToAction("ShowDashboard");
            }

            PostStatusMessage = new(StatusMessageType.Success, Localizer["CheckMultipleChoiceInputAsync:Success"]);
            return RedirectToAction("ShowDashboard");
        }
        catch(CtfApiException ex)
        {
            GetLogger().LogError(ex, "CTF error");

            if(GetAdminMode())
            {
                AddStatusMessage(StatusMessageType.Error, Localizer["CtfError:Admin"]);
                StatusMessages.Add(new(StatusMessageType.Info, ex.FormattedResponseContent ?? "(none)") { Preformatted = true });
                return await ShowDashboardAsync();
            }
            else
            {
                PostStatusMessage = new(StatusMessageType.Error, Localizer["CtfError:Default"]);
                return RedirectToAction("ShowDashboard");
            }
        }
        catch(OperationCanceledException ex)
        {
            GetLogger().LogError(ex, "Check multiple choice input: Timeout");
            PostStatusMessage = new(StatusMessageType.Error, Localizer["CheckMultipleChoiceInputAsync:Timeout"]);

            return RedirectToAction("ShowDashboard");
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Check multiple choice input");

            // Show more details for admins, so they can debug the issue
            if(GetAdminMode())
            {
                AddStatusMessage(StatusMessageType.Error, Localizer["CheckMultipleChoiceInputAsync:UnknownError"]);
                AddStatusMessage(StatusMessageType.Info, Localizer["ExceptionMessage", ex.Message]);
                return await ShowDashboardAsync();
            }

            PostStatusMessage = new(StatusMessageType.Error, Localizer["CheckMultipleChoiceInputAsync:UnknownError"]);
            return RedirectToAction("ShowDashboard");
        }
    }

    [HttpPost("check/script")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckScriptInputAsync(ScriptExerciseInput exerciseInput)
    {
        try
        {
            LastExerciseInput = exerciseInput;

            if(!ModelState.IsValid)
            {
                PostStatusMessage = new(StatusMessageType.Error, Localizer["CheckScriptInputAsync:InvalidInput"]);
                return RedirectToAction("ShowDashboard");
            }

            // Check input
            if(!await CheckInputAsync(exerciseInput.ExerciseId, exerciseInput.Input))
            {
                PostStatusMessage = new(StatusMessageType.Error, Localizer["CheckScriptInputAsync:Wrong"]);
                return RedirectToAction("ShowDashboard");
            }

            PostStatusMessage = new(StatusMessageType.Success, Localizer["CheckScriptInputAsync:Success"]);
            return RedirectToAction("ShowDashboard");
        }
        catch(CtfApiException ex)
        {
            GetLogger().LogError(ex, "CTF error");

            if(GetAdminMode())
            {
                AddStatusMessage(StatusMessageType.Error, Localizer["CtfError:Admin"]);
                StatusMessages.Add(new(StatusMessageType.Info, ex.FormattedResponseContent ?? "(none)") { Preformatted = true });
                return await ShowDashboardAsync();
            }
            else
            {
                PostStatusMessage = new(StatusMessageType.Error, Localizer["CtfError:Default"]);
                return RedirectToAction("ShowDashboard");
            }
        }
        catch(OperationCanceledException ex)
        {
            GetLogger().LogError(ex, "Check script input: Timeout");
            PostStatusMessage = new(StatusMessageType.Error, Localizer["CheckScriptInputAsync:Timeout"]);

            return RedirectToAction("ShowDashboard");
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Check script input");

            // Show more details for admins, so they can debug the issue
            if(GetAdminMode())
            {
                AddStatusMessage(StatusMessageType.Error, Localizer["CheckScriptInputAsync:UnknownError"]);
                AddStatusMessage(StatusMessageType.Info, Localizer["ExceptionMessage", ex.Message]);
                return await ShowDashboardAsync();
            }

            PostStatusMessage = new(StatusMessageType.Error, Localizer["CheckScriptInputAsync:UnknownError"]);
            return RedirectToAction("ShowDashboard");
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
            await stateService.MarkExerciseSolvedAsync(exerciseId, userId, HttpContext.RequestAborted);

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

            PostStatusMessage = new(StatusMessageType.Success, Localizer["MarkExerciseAsSolvedAsync:Success"]);
            return RedirectToAction("ShowDashboard");
        }
        catch(ArgumentException)
        {
            PostStatusMessage = new(StatusMessageType.Error, Localizer["MarkExerciseAsSolvedAsync:NotFound"]);
            return RedirectToAction("ShowDashboard");
        }
        catch(CtfApiException ex)
        {
            GetLogger().LogError(ex, "CTF error");

            AddStatusMessage(StatusMessageType.Error, Localizer["CtfError:Admin"]);
            StatusMessages.Add(new(StatusMessageType.Info, ex.FormattedResponseContent ?? "(none)") { Preformatted = true });
            return await ShowDashboardAsync();
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Mark exercise as solved");
            AddStatusMessage(StatusMessageType.Error, Localizer["MarkExerciseAsSolvedAsync:UnknownError"]);
            AddStatusMessage(StatusMessageType.Info, Localizer["ExceptionMessage", ex.Message]);

            return await ShowDashboardAsync();
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
            await stateService.ResetExerciseStatusAsync(exerciseId, userId, HttpContext.RequestAborted);

            PostStatusMessage = new(StatusMessageType.Success, Localizer["ResetExerciseStatusAsync:Success"]);
            return RedirectToAction("ShowDashboard");
        }
        catch(ArgumentException)
        {
            PostStatusMessage = new(StatusMessageType.Error, Localizer["ResetExerciseStatusAsync:NotFound"]);
            return RedirectToAction("ShowDashboard");
        }
        catch(CtfApiException ex)
        {
            GetLogger().LogError(ex, "CTF error");

            AddStatusMessage(StatusMessageType.Error, Localizer["CtfError:Admin"]);
            StatusMessages.Add(new(StatusMessageType.Info, ex.FormattedResponseContent ?? "(none)") { Preformatted = true });
            return await ShowDashboardAsync();
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Reset exercise");
            AddStatusMessage(StatusMessageType.Error, Localizer["ResetExerciseStatusAsync:UnknownError"]);
            AddStatusMessage(StatusMessageType.Info, Localizer["ExceptionMessage", ex.Message]);

            return await ShowDashboardAsync();
        }
    }
}
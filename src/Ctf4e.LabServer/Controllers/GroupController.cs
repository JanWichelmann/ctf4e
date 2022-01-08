using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ctf4e.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Ctf4e.LabServer.Constants;
using Ctf4e.LabServer.InputModels;
using Ctf4e.LabServer.Options;
using Ctf4e.LabServer.Services;
using Ctf4e.Utilities;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Ctf4e.LabServer.Controllers;

[Route("")]
[Route("group")]
[Authorize]
public class GroupController : ControllerBase
{
    private readonly IStateService _stateService;
    private readonly ICtfApiClient _ctfApiClient;
    private readonly ILabConfigurationService _labConfiguration;
    private readonly IStringLocalizer<GroupController> _localizer;
    private readonly ILogger<GroupController> _logger;

    public GroupController(IStateService stateService, ICtfApiClient ctfApiClient, IOptionsSnapshot<LabOptions> labOptions, ILabConfigurationService labConfiguration, IStringLocalizer<GroupController> localizer, ILogger<GroupController> logger)
        : base("~/Views/Group.cshtml", labOptions, labConfiguration)
    {
        _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
        _ctfApiClient = ctfApiClient ?? throw new ArgumentNullException(nameof(ctfApiClient));
        _labConfiguration = labConfiguration ?? throw new ArgumentNullException(nameof(labConfiguration));
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private async Task<IActionResult> RenderAsync()
    {
        // Retrieve user ID
        int userId = GetCurrentUser().UserId;

        // Pass user scoreboard
        ViewData["Scoreboard"] = await _stateService.GetUserScoreboardAsync(userId);

        return RenderView(MenuItems.Group);
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
        bool correct = await _stateService.CheckInputAsync(exerciseId, userId, input, cts.Token);

        // Notify CTF system
        int? exerciseNumber = _labConfiguration.CurrentConfiguration.Exercises.FirstOrDefault(e => e.Id == exerciseId)?.CtfExerciseNumber;
        if(exerciseNumber != null)
        {
            await _ctfApiClient.CreateExerciseSubmissionAsync(new Ctf4e.Api.Models.ApiExerciseSubmission
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
                AddStatusMessage(_localizer["CheckStringInputAsync:InvalidInput"], StatusMessageTypes.Error);
                return await RenderAsync();
            }
                
            // Check input
            if(!await CheckInputAsync(inputData.ExerciseId, inputData.Input))
            {
                AddStatusMessage(_localizer["CheckStringInputAsync:Wrong"], StatusMessageTypes.Error);
                return await RenderAsync();
            }

            AddStatusMessage(_localizer["CheckStringInputAsync:Success"], StatusMessageTypes.Success);
            return await RenderAsync();
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Check string input");
            AddStatusMessage(_localizer["CheckStringInputAsync:UnknownError"], StatusMessageTypes.Error);
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
                AddStatusMessage(_localizer["CheckMultipleChoiceInputAsync:InvalidInput"], StatusMessageTypes.Error);
                return await RenderAsync();
            }
                
            // Check input
            if(!await CheckInputAsync(inputData.ExerciseId, inputData.SelectedOptions))
            {
                AddStatusMessage(_localizer["CheckMultipleChoiceInputAsync:Wrong"], StatusMessageTypes.Error);
                return await RenderAsync();
            }

            AddStatusMessage(_localizer["CheckMultipleChoiceInputAsync:Success"], StatusMessageTypes.Success);
            return await RenderAsync();
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Check multiple choice input");
            AddStatusMessage(_localizer["CheckMultipleChoiceInputAsync:UnknownError"], StatusMessageTypes.Error);
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
                AddStatusMessage(_localizer["CheckScriptInputAsync:InvalidInput"], StatusMessageTypes.Error);
                return await RenderAsync();
            }
                
            // Check input
            if(!await CheckInputAsync(inputData.ExerciseId, inputData.Input))
            {
                AddStatusMessage(_localizer["CheckScriptInputAsync:Wrong"], StatusMessageTypes.Error);
                return await RenderAsync();
            }

            AddStatusMessage(_localizer["CheckScriptInputAsync:Success"], StatusMessageTypes.Success);
            return await RenderAsync();
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Check script input");
            AddStatusMessage(_localizer["CheckScriptInputAsync:UnknownError"], StatusMessageTypes.Error);
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
            await _stateService.MarkExerciseSolvedAsync(exerciseId, userId);

            // Notify CTF system
            int? exerciseNumber = _labConfiguration.CurrentConfiguration.Exercises.FirstOrDefault(e => e.Id == exerciseId)?.CtfExerciseNumber;
            if(exerciseNumber != null)
            {
                await _ctfApiClient.CreateExerciseSubmissionAsync(new Ctf4e.Api.Models.ApiExerciseSubmission
                {
                    ExerciseNumber = exerciseNumber.Value,
                    UserId = userId,
                    ExercisePassed = true,
                    Weight = 1,
                    SubmissionTime = DateTime.Now
                }, CancellationToken.None);
            }

            AddStatusMessage(_localizer["MarkExerciseAsSolvedAsync:Success"], StatusMessageTypes.Success);
            return await RenderAsync();
        }
        catch(ArgumentException)
        {
            AddStatusMessage(_localizer["MarkExerciseAsSolvedAsync:NotFound"], StatusMessageTypes.Error);
            return await RenderAsync();
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Mark exercise as solved");
            AddStatusMessage(_localizer["MarkExerciseAsSolvedAsync:UnknownError"], StatusMessageTypes.Error);
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
            await _stateService.ResetExerciseStatusAsync(exerciseId, userId);

            AddStatusMessage(_localizer["ResetExerciseStatusAsync:Success"], StatusMessageTypes.Success);
            return await RenderAsync();
        }
        catch(ArgumentException)
        {
            AddStatusMessage(_localizer["ResetExerciseStatusAsync:NotFound"], StatusMessageTypes.Error);
            return await RenderAsync();
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Reset exercise");
            AddStatusMessage(_localizer["ResetExerciseStatusAsync:UnknownError"], StatusMessageTypes.Error);
            return await RenderAsync();
        }
    }
}
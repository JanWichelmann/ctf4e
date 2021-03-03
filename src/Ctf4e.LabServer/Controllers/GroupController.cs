using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ctf4e.Api.Services;
using Ctf4e.LabServer.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Ctf4e.LabServer.Constants;
using Ctf4e.LabServer.InputModels;
using Ctf4e.LabServer.Options;
using Ctf4e.LabServer.Services;
using Ctf4e.Utilities;
using Microsoft.Extensions.Logging;

namespace Ctf4e.LabServer.Controllers
{
    [Route("")]
    [Route("group")]
    [Authorize]
    public class GroupController : ControllerBase
    {
        private readonly IStateService _stateService;
        private readonly ICtfApiClient _ctfApiClient;
        private readonly IOptionsSnapshot<LabOptions> _labOptions;
        private readonly ILabConfigurationService _labConfiguration;
        private readonly ILogger<GroupController> _logger;

        public GroupController(IStateService stateService, ICtfApiClient ctfApiClient, IOptionsSnapshot<LabOptions> labOptions, ILabConfigurationService labConfiguration, ILogger<GroupController> logger)
            : base("~/Views/Group.cshtml", labOptions, labConfiguration)
        {
            _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
            _ctfApiClient = ctfApiClient ?? throw new ArgumentNullException(nameof(ctfApiClient));
            _labOptions = labOptions ?? throw new ArgumentNullException(nameof(labOptions));
            _labConfiguration = labConfiguration ?? throw new ArgumentNullException(nameof(labConfiguration));
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
                    AddStatusMessage("Ungültige Eingabe.", StatusMessageTypes.Error);
                    return await RenderAsync();
                }
                
                // Check input
                if(!await CheckInputAsync(inputData.ExerciseId, inputData.Input))
                {
                    AddStatusMessage("Diese Lösung ist nicht korrekt.", StatusMessageTypes.Error);
                    return await RenderAsync();
                }

                AddStatusMessage("Die Aufgabe wurde korrekt gelöst!", StatusMessageTypes.Success);
                return await RenderAsync();
            }
            catch(Exception ex)
            {
                AddStatusMessage("Ein Fehler ist aufgetreten.", StatusMessageTypes.Error);
                _logger.LogError(ex, "An error occured during evaluation of a solution attempt");
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
                    AddStatusMessage("Ungültige Eingabe.", StatusMessageTypes.Error);
                    return await RenderAsync();
                }
                
                // Check input
                if(!await CheckInputAsync(inputData.ExerciseId, inputData.SelectedOptions))
                {
                    AddStatusMessage("Diese Lösung ist nicht korrekt.", StatusMessageTypes.Error);
                    return await RenderAsync();
                }

                AddStatusMessage("Die Aufgabe wurde korrekt gelöst!", StatusMessageTypes.Success);
                return await RenderAsync();
            }
            catch(Exception ex)
            {
                AddStatusMessage("Ein Fehler ist aufgetreten.", StatusMessageTypes.Error);
                _logger.LogError(ex, "An error occured during evaluation of a solution attempt");
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
                    AddStatusMessage("Ungültige Eingabe.", StatusMessageTypes.Error);
                    return await RenderAsync();
                }
                
                // Check input
                if(!await CheckInputAsync(inputData.ExerciseId, inputData.Input))
                {
                    AddStatusMessage("Diese Lösung ist nicht korrekt.", StatusMessageTypes.Error);
                    return await RenderAsync();
                }

                AddStatusMessage("Die Aufgabe wurde korrekt gelöst!", StatusMessageTypes.Success);
                return await RenderAsync();
            }
            catch(Exception ex)
            {
                AddStatusMessage("Ein Fehler ist aufgetreten.", StatusMessageTypes.Error);
                _logger.LogError(ex, "An error occured during evaluation of a solution attempt");
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
                    });
                }

                AddStatusMessage("Die Aufgabe wurde erfolgreich als gelöst markiert.", StatusMessageTypes.Success);
                return await RenderAsync();
            }
            catch(ArgumentException)
            {
                AddStatusMessage("Diese Aufgabe existiert nicht.", StatusMessageTypes.Error);
                return await RenderAsync();
            }
            catch(Exception ex)
            {
                AddStatusMessage("Ein Fehler ist aufgetreten: " + ex.Message, StatusMessageTypes.Error);
                _logger.LogError(ex, "Could not mark exercise as solved");
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

                AddStatusMessage("Die Aufgabe wurde erfolgreich zurückgesetzt.", StatusMessageTypes.Success);
                return await RenderAsync();
            }
            catch(ArgumentException)
            {
                AddStatusMessage("Diese Aufgabe existiert nicht.", StatusMessageTypes.Error);
                return await RenderAsync();
            }
            catch(Exception ex)
            {
                AddStatusMessage("Ein Fehler ist aufgetreten: " + ex.Message, StatusMessageTypes.Error);
                _logger.LogError(ex, "Could not reset exercise");
                return await RenderAsync();
            }
        }
    }
}
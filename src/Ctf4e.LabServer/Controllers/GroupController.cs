using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ctf4e.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Ctf4e.LabServer.Constants;
using Ctf4e.LabServer.InputModels;
using Ctf4e.LabServer.Options;
using Ctf4e.LabServer.Services;
using Ctf4e.Utilities;

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

        public GroupController(IStateService stateService, ICtfApiClient ctfApiClient, IOptionsSnapshot<LabOptions> labOptions)
            : base("~/Views/Group.cshtml", labOptions)
        {
            _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
            _ctfApiClient = ctfApiClient ?? throw new ArgumentNullException(nameof(ctfApiClient));
            _labOptions = labOptions ?? throw new ArgumentNullException(nameof(labOptions));
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

        [HttpPost("check")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckInputAsync(ExerciseInputData exerciseInputData)
        {
            try
            {
                // Get current user
                int userId = GetCurrentUser().UserId;
                
                // Check input
                bool correct = await _stateService.CheckInputAsync(exerciseInputData.ExerciseId, userId, exerciseInputData.Input);

                // Notify CTF system
                int? exerciseNumber = _labOptions.Value.Exercises.FirstOrDefault(e => e.Id == exerciseInputData.ExerciseId)?.CtfExerciseNumber;
                if(exerciseNumber != null)
                {
                    await _ctfApiClient.CreateExerciseSubmissionAsync(new Ctf4e.Api.Models.ApiExerciseSubmission
                    {
                        ExerciseNumber = exerciseNumber.Value,
                        UserId = userId,
                        ExercisePassed = correct,
                        Weight = 1,
                        SubmissionTime = DateTime.Now
                    });
                }

                if(!correct)
                {
                    AddStatusMessage("Diese Lösung ist nicht korrekt.", StatusMessageTypes.Error);
                    ViewData["LastInput"] = exerciseInputData;
                    return await RenderAsync();
                }

                AddStatusMessage("Die Aufgabe wurde korrekt gelöst!", StatusMessageTypes.Success);
                return await RenderAsync();
            }
            catch(ArgumentException)
            {
                AddStatusMessage("Diese Aufgabe existiert nicht.", StatusMessageTypes.Error);
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
                int? exerciseNumber = _labOptions.Value.Exercises.FirstOrDefault(e => e.Id == exerciseId)?.CtfExerciseNumber;
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
        }
    }
}

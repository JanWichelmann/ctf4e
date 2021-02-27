using System;
using System.Linq;
using System.Threading.Tasks;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Models;
using Ctf4e.Server.Options;
using Ctf4e.Server.Services;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ctf4e.Server.Controllers
{
    [Route("admin/exercises")]
    [Authorize(Policy = AuthenticationStrings.PolicyIsAdmin)]
    public class AdminExercisesController : ControllerBase
    {
        private readonly IExerciseService _exerciseService;
        private readonly ILabService _labService;

        public AdminExercisesController(IUserService userService, IOptions<MainOptions> mainOptions, IExerciseService exerciseService, ILabService labService)
            : base("~/Views/AdminExercises.cshtml", userService, mainOptions)
        {
            _exerciseService = exerciseService ?? throw new ArgumentNullException(nameof(exerciseService));
            _labService = labService ?? throw new ArgumentNullException(nameof(labService));
        }

        private async Task<IActionResult> RenderAsync(ViewType viewType, int labId, object model)
        {
            var lab = await _labService.GetLabAsync(labId, HttpContext.RequestAborted);
            if(lab == null)
                return RedirectToAction("RenderLabList", "AdminLabs");
            ViewData["Lab"] = await _labService.GetLabAsync(labId, HttpContext.RequestAborted);

            ViewData["ViewType"] = viewType;
            return await RenderViewAsync(MenuItems.AdminExercises, model);
        }

        [HttpGet]
        public async Task<IActionResult> RenderExerciseListAsync(int labId)
        {
            var exercises = await _exerciseService.GetExercisesAsync(labId).ToListAsync();

            return await RenderAsync(ViewType.List, labId, exercises);
        }

        private async Task<IActionResult> ShowEditExerciseFormAsync(int? id, Exercise exercise = null)
        {
            // Retrieve by ID, if no object from a failed POST was passed
            if(id != null)
            {
                exercise = await _exerciseService.GetExerciseAsync(id.Value, HttpContext.RequestAborted);
                if(exercise == null)
                    return this.RedirectToAction("RenderLabList", "AdminLabs");
            }

            if(exercise == null)
                return this.RedirectToAction("RenderLabList", "AdminLabs");

            return await RenderAsync(ViewType.Edit, exercise.LabId, exercise);
        }

        [HttpGet("edit")]
        public Task<IActionResult> ShowEditExerciseFormAsync(int id)
        {
            return ShowEditExerciseFormAsync(id, null);
        }

        [HttpPost("edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditExerciseAsync(Exercise exerciseData)
        {
            // Check input
            if(!ModelState.IsValid)
            {
                AddStatusMessage("Ungültige Eingabe.", StatusMessageTypes.Error);
                return await ShowEditExerciseFormAsync(null, exerciseData);
            }

            try
            {
                // Retrieve edited exercise from database and apply changes
                var exercise = await _exerciseService.GetExerciseAsync(exerciseData.Id, HttpContext.RequestAborted);
                exercise.ExerciseNumber = exerciseData.ExerciseNumber;
                exercise.Name = exerciseData.Name;
                exercise.IsMandatory = exerciseData.IsMandatory;
                exercise.IsPreStartAvailable = exerciseData.IsPreStartAvailable;
                exercise.BasePoints = exerciseData.BasePoints;
                exercise.PenaltyPoints = exerciseData.PenaltyPoints;
                await _exerciseService.UpdateExerciseAsync(exercise, HttpContext.RequestAborted);

                AddStatusMessage("Änderungen gespeichert.", StatusMessageTypes.Success);
                return await RenderExerciseListAsync(exercise.LabId);
            }
            catch(InvalidOperationException ex)
            {
                AddStatusMessage(ex.Message, StatusMessageTypes.Error);
                return await ShowEditExerciseFormAsync(null, exerciseData);
            }
        }

        [HttpGet("create")]
        public async Task<IActionResult> ShowCreateExerciseFormAsync(int labId, Exercise exercise = null)
        {
            return await RenderAsync(ViewType.Create, labId, exercise);
        }

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateExerciseAsync(Exercise exerciseData)
        {
            // Check input
            if(!ModelState.IsValid)
            {
                AddStatusMessage("Ungültige Eingabe.", StatusMessageTypes.Error);
                return await ShowCreateExerciseFormAsync(exerciseData.LabId, exerciseData);
            }

            try
            {
                // Create exercise
                var exercise = new Exercise
                {
                    LabId = exerciseData.LabId,
                    ExerciseNumber = exerciseData.ExerciseNumber,
                    Name = exerciseData.Name,
                    IsMandatory = exerciseData.IsMandatory,
                    IsPreStartAvailable = exerciseData.IsPreStartAvailable,
                    BasePoints = exerciseData.BasePoints,
                    PenaltyPoints = exerciseData.PenaltyPoints
                };
                await _exerciseService.CreateExerciseAsync(exercise, HttpContext.RequestAborted);

                AddStatusMessage("Die Aufgabe wurde erfolgreich erstellt.", StatusMessageTypes.Success);
                return await RenderExerciseListAsync(exerciseData.LabId);
            }
            catch(InvalidOperationException ex)
            {
                AddStatusMessage(ex.Message, StatusMessageTypes.Error);
                return await ShowCreateExerciseFormAsync(exerciseData.LabId, exerciseData);
            }
        }

        [HttpPost("delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteExerciseAsync(int id)
        {
            // Input check
            var exercise = await _exerciseService.GetExerciseAsync(id, HttpContext.RequestAborted);
            if(exercise == null)
                return this.RedirectToAction("RenderLabList", "AdminLabs");

            try
            {
                // Delete exercise
                await _exerciseService.DeleteExerciseAsync(id, HttpContext.RequestAborted);

                AddStatusMessage("Die Aufgabe wurde erfolgreich gelöscht.", StatusMessageTypes.Success);
            }
            catch(Exception ex)
            {
                AddStatusMessage(ex.ToString(), StatusMessageTypes.Error);
            }

            return await RenderExerciseListAsync(exercise.LabId);
        }

        public enum ViewType
        {
            List,
            Edit,
            Create
        }
    }
}
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
        private readonly ILessonService _lessonService;

        public AdminExercisesController(IUserService userService, IOptions<MainOptions> mainOptions, IExerciseService exerciseService, ILessonService lessonService)
            : base("~/Views/AdminExercises.cshtml", userService, mainOptions)
        {
            _exerciseService = exerciseService ?? throw new ArgumentNullException(nameof(exerciseService));
            _lessonService = lessonService ?? throw new ArgumentNullException(nameof(lessonService));
        }

        private async Task<IActionResult> RenderAsync(ViewType viewType, int lessonId, object model)
        {
            var lesson = await _lessonService.GetLessonAsync(lessonId, HttpContext.RequestAborted);
            if(lesson == null)
                return RedirectToAction("RenderLessonList", "AdminLessons");
            ViewData["Lesson"] = await _lessonService.GetLessonAsync(lessonId, HttpContext.RequestAborted);

            ViewData["ViewType"] = viewType;
            return await RenderViewAsync(MenuItems.AdminExercises, model);
        }

        [HttpGet]
        public async Task<IActionResult> RenderExerciseListAsync(int lessonId)
        {
            var exercises = await _exerciseService.GetExercisesAsync(lessonId).ToListAsync();

            return await RenderAsync(ViewType.List, lessonId, exercises);
        }

        private async Task<IActionResult> ShowEditExerciseFormAsync(int? id, Exercise exercise = null)
        {
            // Retrieve by ID, if no object from a failed POST was passed
            if(id != null)
            {
                exercise = await _exerciseService.GetExerciseAsync(id.Value, HttpContext.RequestAborted);
                if(exercise == null)
                    return this.RedirectToAction("RenderLessonList", "AdminLessons");
            }

            if(exercise == null)
                return this.RedirectToAction("RenderLessonList", "AdminLessons");

            return await RenderAsync(ViewType.Edit, exercise.LessonId, exercise);
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
                return await RenderExerciseListAsync(exercise.LessonId);
            }
            catch(InvalidOperationException ex)
            {
                AddStatusMessage(ex.Message, StatusMessageTypes.Error);
                return await ShowEditExerciseFormAsync(null, exerciseData);
            }
        }

        [HttpGet("create")]
        public async Task<IActionResult> ShowCreateExerciseFormAsync(int lessonId, Exercise exercise = null)
        {
            return await RenderAsync(ViewType.Create, lessonId, exercise);
        }

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateExerciseAsync(Exercise exerciseData)
        {
            // Check input
            if(!ModelState.IsValid)
            {
                AddStatusMessage("Ungültige Eingabe.", StatusMessageTypes.Error);
                return await ShowCreateExerciseFormAsync(exerciseData.LessonId, exerciseData);
            }

            try
            {
                // Create exercise
                var exercise = new Exercise
                {
                    LessonId = exerciseData.LessonId,
                    ExerciseNumber = exerciseData.ExerciseNumber,
                    Name = exerciseData.Name,
                    IsMandatory = exerciseData.IsMandatory,
                    IsPreStartAvailable = exerciseData.IsPreStartAvailable,
                    BasePoints = exerciseData.BasePoints,
                    PenaltyPoints = exerciseData.PenaltyPoints
                };
                await _exerciseService.CreateExerciseAsync(exercise, HttpContext.RequestAborted);

                AddStatusMessage("Die Aufgabe wurde erfolgreich erstellt.", StatusMessageTypes.Success);
                return await RenderExerciseListAsync(exerciseData.LessonId);
            }
            catch(InvalidOperationException ex)
            {
                AddStatusMessage(ex.Message, StatusMessageTypes.Error);
                return await ShowCreateExerciseFormAsync(exerciseData.LessonId, exerciseData);
            }
        }

        [HttpPost("delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteExerciseAsync(int id)
        {
            // Input check
            var exercise = await _exerciseService.GetExerciseAsync(id, HttpContext.RequestAborted);
            if(exercise == null)
                return this.RedirectToAction("RenderLessonList", "AdminLessons");

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

            return await RenderExerciseListAsync(exercise.LessonId);
        }

        public enum ViewType
        {
            List,
            Edit,
            Create
        }
    }
}
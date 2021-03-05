using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Ctf4e.Api;
using Ctf4e.Api.Models;
using Ctf4e.Api.Services;
using Ctf4e.Server.Models;
using Ctf4e.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Ctf4e.Server.Controllers
{
    [Route("api")]
    [ApiController]
    public class ApiController : Controller
    {
        private readonly ILessonService _lessonService;
        private readonly IExerciseService _exerciseService;
        private readonly IUserService _userService;
        private readonly ILessonExecutionService _lessonExecutionService;


        public ApiController(ILessonService lessonService, IExerciseService exerciseService, IUserService userService, ILessonExecutionService lessonExecutionService)
        {
            _lessonService = lessonService ?? throw new ArgumentNullException(nameof(lessonService));
            _exerciseService = exerciseService ?? throw new ArgumentNullException(nameof(exerciseService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _lessonExecutionService = lessonExecutionService ?? throw new ArgumentNullException(nameof(lessonExecutionService));
        }

        [HttpPost("exercisesubmission/create")]
        public async Task<IActionResult> CreateExerciseSubmissionAsync(CtfApiRequest request)
        {
            try
            {
                // Resolve lesson
                var lesson = await _lessonService.GetLessonAsync(request.LessonId, HttpContext.RequestAborted);
                if(lesson == null)
                    return BadRequest();

                // Decode request
                var apiExerciseSubmission = request.Decode<ApiExerciseSubmission>(new CryptoService(lesson.ApiCode));

                // Resolve exercise
                var exercise = await _exerciseService.FindExerciseAsync(lesson.Id, apiExerciseSubmission.ExerciseNumber, HttpContext.RequestAborted);
                if(exercise == null)
                    return NotFound(new { error = "Exercise not found" });

                // Check lesson execution
                // This will also automatically check whether the given user exists
                var lessonExecution = await _lessonExecutionService.GetLessonExecutionForUserAsync(apiExerciseSubmission.UserId, lesson.Id, HttpContext.RequestAborted);
                var now = DateTime.Now;
                if(lessonExecution == null || now < lessonExecution.PreStart)
                    return NotFound(new { error = "Lesson is not active for this user" });

                // Some exercises may only be submitted after the pre-start phase has ended
                if(!exercise.IsPreStartAvailable && now < lessonExecution.Start)
                    return NotFound(new { error = "This exercise may not be submitted in the pre-start phase" });

                // Create submission
                var submission = new ExerciseSubmission
                {
                    ExerciseId = exercise.Id,
                    UserId = apiExerciseSubmission.UserId,
                    ExercisePassed = apiExerciseSubmission.ExercisePassed,
                    SubmissionTime = apiExerciseSubmission.SubmissionTime ?? DateTime.Now,
                    Weight = apiExerciseSubmission.ExercisePassed ? 1 : (apiExerciseSubmission.Weight >= 0 ? apiExerciseSubmission.Weight : 1)
                };
                await _exerciseService.CreateExerciseSubmissionAsync(submission, HttpContext.RequestAborted);

                return Ok();
            }
            catch(CryptographicException ex)
            {
                return StatusCode(StatusCodes.Status401Unauthorized, new { error = ex.ToString() });
            }
            catch(Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.ToString() });
            }
        }

        [HttpPost("exercisesubmission/clear")]
        public async Task<IActionResult> ClearExerciseSubmissionsAsync(CtfApiRequest request)
        {
            try
            {
                // Resolve lesson
                var lesson = await _lessonService.GetLessonAsync(request.LessonId, HttpContext.RequestAborted);
                if(lesson == null)
                    return BadRequest();

                // Decode request
                var apiExerciseSubmission = request.Decode<ApiExerciseSubmission>(new CryptoService(lesson.ApiCode));

                // Resolve exercise
                var exercise = await _exerciseService.FindExerciseAsync(lesson.Id, apiExerciseSubmission.ExerciseNumber, HttpContext.RequestAborted);
                if(exercise == null)
                    return NotFound(new { error = "Exercise not found" });

                // Check user
                if(!await _userService.UserExistsAsync(apiExerciseSubmission.UserId, HttpContext.RequestAborted))
                    return NotFound(new { error = "User not found" });

                // Clear exercise submissions
                await _exerciseService.ClearExerciseSubmissionsAsync(exercise.Id, apiExerciseSubmission.UserId, HttpContext.RequestAborted);

                return Ok();
            }
            catch(CryptographicException ex)
            {
                return StatusCode(StatusCodes.Status401Unauthorized, new { error = ex.ToString() });
            }
            catch(Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.ToString() });
            }
        }
    }
}
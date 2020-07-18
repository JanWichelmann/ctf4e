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
        private readonly ILabService _labService;
        private readonly IExerciseService _exerciseService;
        private readonly IUserService _userService;


        public ApiController(ILabService labService, IExerciseService exerciseService, IUserService userService)
        {
            _labService = labService ?? throw new ArgumentNullException(nameof(labService));
            _exerciseService = exerciseService ?? throw new ArgumentNullException(nameof(exerciseService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        }

        [HttpPost("exercisesubmission/create")]
        public async Task<IActionResult> CreateExerciseSubmissionAsync(CtfApiRequest request)
        {
            try
            {
                // Resolve lab
                var lab = await _labService.GetLabAsync(request.LabId, HttpContext.RequestAborted);
                if(lab == null)
                    return BadRequest();

                // Decode request
                var apiExerciseSubmission = request.Decode<ApiExerciseSubmission>(new CryptoService(lab.ApiCode));

                // Resolve exercise
                var exercise = await _exerciseService.FindExerciseAsync(lab.Id, apiExerciseSubmission.ExerciseNumber, HttpContext.RequestAborted);
                if(exercise == null)
                    return NotFound(new { error = "Exercise not found" });

                // Check group
                if(!await _userService.GroupExistsAsync(apiExerciseSubmission.GroupId, HttpContext.RequestAborted))
                    return NotFound(new { error = "Group not found" });

                // Create submission
                var submission = new ExerciseSubmission
                {
                    ExerciseId = exercise.Id,
                    GroupId = apiExerciseSubmission.GroupId,
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
                // Resolve lab
                var lab = await _labService.GetLabAsync(request.LabId, HttpContext.RequestAborted);
                if(lab == null)
                    return BadRequest();

                // Decode request
                var apiExerciseSubmission = request.Decode<ApiExerciseSubmission>(new CryptoService(lab.ApiCode));

                // Resolve exercise
                var exercise = await _exerciseService.FindExerciseAsync(lab.Id, apiExerciseSubmission.ExerciseNumber, HttpContext.RequestAborted);
                if(exercise == null)
                    return NotFound(new { error = "Exercise not found" });

                // Check group
                if(!await _userService.GroupExistsAsync(apiExerciseSubmission.GroupId, HttpContext.RequestAborted))
                    return NotFound(new { error = "Group not found" });

                // Clear exercise submissions
                await _exerciseService.ClearExerciseSubmissionsAsync(exercise.Id, apiExerciseSubmission.GroupId, HttpContext.RequestAborted);

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

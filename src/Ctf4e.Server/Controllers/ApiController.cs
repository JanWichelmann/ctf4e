using System;
using System.Linq;
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
        private readonly ILabExecutionService _labExecutionService;


        public ApiController(ILabService labService, IExerciseService exerciseService, IUserService userService, ILabExecutionService labExecutionService)
        {
            _labService = labService ?? throw new ArgumentNullException(nameof(labService));
            _exerciseService = exerciseService ?? throw new ArgumentNullException(nameof(exerciseService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _labExecutionService = labExecutionService ?? throw new ArgumentNullException(nameof(labExecutionService));
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

                // Check lab execution
                // This will also automatically check whether the given user exists
                var labExecution = await _labExecutionService.GetLabExecutionForUserAsync(apiExerciseSubmission.UserId, lab.Id, HttpContext.RequestAborted);
                var now = DateTime.Now;
                if(labExecution == null || now < labExecution.PreStart)
                    return NotFound(new { error = "Lab is not active for this user" });

                // Some exercises may only be submitted after the pre-start phase has ended
                if(!exercise.IsPreStartAvailable && now < labExecution.Start)
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

        [HttpPost("exercisesubmission-group/create")]
        public async Task<IActionResult> CreateGroupExerciseSubmissionAsync(CtfApiRequest request)
        {
            try
            {
                // Resolve lab
                var lab = await _labService.GetLabAsync(request.LabId, HttpContext.RequestAborted);
                if(lab == null)
                    return BadRequest();

                // Decode request
                var apiExerciseSubmission = request.Decode<ApiGroupExerciseSubmission>(new CryptoService(lab.ApiCode));

                // Resolve exercise
                var exercise = await _exerciseService.FindExerciseAsync(lab.Id, apiExerciseSubmission.ExerciseNumber, HttpContext.RequestAborted);
                if(exercise == null)
                    return NotFound(new { error = "Exercise not found" });

                // Check lab execution
                // This will also automatically check whether the given group exists
                var labExecution = await _labExecutionService.GetLabExecutionAsync(apiExerciseSubmission.GroupId, lab.Id, HttpContext.RequestAborted);
                var now = DateTime.Now;
                if(labExecution == null || now < labExecution.PreStart)
                    return NotFound(new { error = "Lab is not active for this group" });

                // Some exercises may only be submitted after the pre-start phase has ended
                if(!exercise.IsPreStartAvailable && now < labExecution.Start)
                    return NotFound(new { error = "This exercise may not be submitted in the pre-start phase" });

                // Create submission for each group member
                var groupMembers = await _userService.GetGroupMembersAsync(apiExerciseSubmission.GroupId).ToListAsync(HttpContext.RequestAborted);
                foreach(var groupMember in groupMembers)
                {
                    var submission = new ExerciseSubmission
                    {
                        ExerciseId = exercise.Id,
                        UserId = groupMember.Id,
                        ExercisePassed = apiExerciseSubmission.ExercisePassed,
                        SubmissionTime = apiExerciseSubmission.SubmissionTime ?? DateTime.Now,
                        Weight = apiExerciseSubmission.ExercisePassed ? 1 : (apiExerciseSubmission.Weight >= 0 ? apiExerciseSubmission.Weight : 1)
                    };
                    await _exerciseService.CreateExerciseSubmissionAsync(submission, HttpContext.RequestAborted);
                    
                    // If the exercise is not passed with weight > 0, do only insert it for a single group member
                    // Else, the penalty points would be applied for each group member
                    if(!apiExerciseSubmission.ExercisePassed && apiExerciseSubmission.Weight > 0)
                        break;
                }

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

        [HttpPost("exercisesubmission-group/clear")]
        public async Task<IActionResult> ClearGroupExerciseSubmissionsAsync(CtfApiRequest request)
        {
            try
            {
                // Resolve lab
                var lab = await _labService.GetLabAsync(request.LabId, HttpContext.RequestAborted);
                if(lab == null)
                    return BadRequest();

                // Decode request
                var apiExerciseSubmission = request.Decode<ApiGroupExerciseSubmission>(new CryptoService(lab.ApiCode));

                // Resolve exercise
                var exercise = await _exerciseService.FindExerciseAsync(lab.Id, apiExerciseSubmission.ExerciseNumber, HttpContext.RequestAborted);
                if(exercise == null)
                    return NotFound(new { error = "Exercise not found" });

                // Get group members
                var groupMembers = await _userService.GetGroupMembersAsync(apiExerciseSubmission.GroupId).ToListAsync(HttpContext.RequestAborted);
                if(!groupMembers.Any())
                    return NotFound(new { error = "Empty or not existing group" });

                // Clear exercise submissions for each group member
                foreach(var groupMember in groupMembers)
                {
                    await _exerciseService.ClearExerciseSubmissionsAsync(exercise.Id, groupMember.Id, HttpContext.RequestAborted);
                }

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
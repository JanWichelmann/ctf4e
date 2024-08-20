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
using Microsoft.Extensions.Logging;

namespace Ctf4e.Server.Controllers;

[Route("api")]
[ApiController]
public class ApiController : Controller
{
    private readonly ILabService _labService;
    private readonly IExerciseService _exerciseService;
    private readonly IUserService _userService;
    private readonly ILabExecutionService _labExecutionService;
    private readonly ILogger<ApiController> _logger;


    public ApiController(ILabService labService, IExerciseService exerciseService, IUserService userService, ILabExecutionService labExecutionService, ILogger<ApiController> logger)
    {
        _labService = labService ?? throw new ArgumentNullException(nameof(labService));
        _exerciseService = exerciseService ?? throw new ArgumentNullException(nameof(exerciseService));
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        _labExecutionService = labExecutionService ?? throw new ArgumentNullException(nameof(labExecutionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost("exercisesubmission/create")]
    public async Task<IActionResult> CreateExerciseSubmissionAsync(CtfApiRequest request)
    {
        try
        {
            // Resolve lab
            var lab = await _labService.FindLabByIdAsync(request.LabId, HttpContext.RequestAborted);
            if(lab == null)
                return BadRequest(new { error = $"Could not resolve requested lab {request.LabId}"});

            // Decode request
            var apiExerciseSubmission = request.Decode<ApiExerciseSubmission>(new CryptoService(lab.ApiCode));

            // Resolve exercise
            var exercise = await _exerciseService.FindExerciseByNumberAsync(lab.Id, apiExerciseSubmission.ExerciseNumber, HttpContext.RequestAborted);
            if(exercise == null)
                return NotFound(new { error = "Exercise not found" });

            // Check lab execution
            // This will also automatically check whether the given user exists
            var labExecution = await _labExecutionService.FindLabExecutionByUserAndLabAsync(apiExerciseSubmission.UserId, lab.Id, HttpContext.RequestAborted);
            var now = DateTime.Now;
            if(labExecution == null || now < labExecution.Start)
                return NotFound(new { error = "Lab is not active for this user" });

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
            _logger.LogError(ex, "Create exercise submission for user");
            return StatusCode(StatusCodes.Status401Unauthorized, new { error = "Could not decode the request packet" });
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Create exercise submission for user");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An internal error occured during processing of the request" });
        }
    }

    [HttpPost("exercisesubmission/clear")]
    public async Task<IActionResult> ClearExerciseSubmissionsAsync(CtfApiRequest request)
    {
        try
        {
            // Resolve lab
            var lab = await _labService.FindLabByIdAsync(request.LabId, HttpContext.RequestAborted);
            if(lab == null)
                return BadRequest(new { error = $"Could not resolve requested lab {request.LabId}"});

            // Decode request
            var apiExerciseSubmission = request.Decode<ApiExerciseSubmission>(new CryptoService(lab.ApiCode));

            // Resolve exercise
            var exercise = await _exerciseService.FindExerciseByNumberAsync(lab.Id, apiExerciseSubmission.ExerciseNumber, HttpContext.RequestAborted);
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
            _logger.LogError(ex, "Clear exercise submissions of user");
            return StatusCode(StatusCodes.Status401Unauthorized, new { error = "Could not decode the request packet" });
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Create exercise submissions of user");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An internal error occured during processing of the request" });
        }
    }

    [HttpPost("exercisesubmission-group/create")]
    public async Task<IActionResult> CreateGroupExerciseSubmissionAsync(CtfApiRequest request)
    {
        try
        {
            // Resolve lab
            var lab = await _labService.FindLabByIdAsync(request.LabId, HttpContext.RequestAborted);
            if(lab == null)
                return BadRequest(new { error = $"Could not resolve requested lab {request.LabId}"});

            // Decode request
            var apiExerciseSubmission = request.Decode<ApiGroupExerciseSubmission>(new CryptoService(lab.ApiCode));

            // Resolve exercise
            var exercise = await _exerciseService.FindExerciseByNumberAsync(lab.Id, apiExerciseSubmission.ExerciseNumber, HttpContext.RequestAborted);
            if(exercise == null)
                return NotFound(new { error = "Exercise not found" });

            // Check lab execution
            // This will also automatically check whether the given group exists
            var labExecution = await _labExecutionService.FindLabExecutionAsync(apiExerciseSubmission.GroupId, lab.Id, HttpContext.RequestAborted);
            var now = DateTime.Now;
            if(labExecution == null || now < labExecution.Start)
                return NotFound(new { error = "Lab is not active for this group" });

            // Create submission for each group member
            var groupMembers = await _userService.GetGroupMembersAsync(apiExerciseSubmission.GroupId, HttpContext.RequestAborted);
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
            _logger.LogError(ex, "Create exercise submission for group");
            return StatusCode(StatusCodes.Status401Unauthorized, new { error = "Could not decode the request packet" });
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Create exercise submission for group");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An internal error occured during processing of the request" });
        }
    }

    [HttpPost("exercisesubmission-group/clear")]
    public async Task<IActionResult> ClearGroupExerciseSubmissionsAsync(CtfApiRequest request)
    {
        try
        {
            // Resolve lab
            var lab = await _labService.FindLabByIdAsync(request.LabId, HttpContext.RequestAborted);
            if(lab == null)
                return BadRequest(new { error = $"Could not resolve requested lab {request.LabId}"});

            // Decode request
            var apiExerciseSubmission = request.Decode<ApiGroupExerciseSubmission>(new CryptoService(lab.ApiCode));

            // Resolve exercise
            var exercise = await _exerciseService.FindExerciseByNumberAsync(lab.Id, apiExerciseSubmission.ExerciseNumber, HttpContext.RequestAborted);
            if(exercise == null)
                return NotFound(new { error = "Exercise not found" });

            // Get group members
            var groupMembers = await _userService.GetGroupMembersAsync(apiExerciseSubmission.GroupId, HttpContext.RequestAborted);
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
            _logger.LogError(ex, "Clear exercise submission of group");
            return StatusCode(StatusCodes.Status401Unauthorized, new { error = "Could not decode the request packet" });
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Clear exercise submission of group");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An internal error occured during processing of the request" });
        }
    }
}
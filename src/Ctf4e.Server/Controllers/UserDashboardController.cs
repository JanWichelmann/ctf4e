using System;
using System.Threading.Tasks;
using Ctf4e.Api;
using Ctf4e.Api.Models;
using Ctf4e.Api.Services;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Options;
using Ctf4e.Server.Services;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ctf4e.Server.Controllers
{
    [Route("dashboard")]
    [Authorize(Policy = AuthenticationStrings.PolicyIsGroupMember)]
    public class UserDashboardController : ControllerBase
    {
        private readonly IScoreboardService _scoreboardService;
        private readonly ILessonExecutionService _lessonExecutionService;
        private readonly IFlagService _flagService;
        private readonly ILessonService _lessonService;

        public UserDashboardController(IUserService userService, IOptions<MainOptions> mainOptions, IScoreboardService scoreboardService, ILessonExecutionService lessonExecutionService, IFlagService flagService, ILessonService lessonService)
            : base("~/Views/UserDashboard.cshtml", userService, mainOptions)
        {
            _scoreboardService = scoreboardService ?? throw new ArgumentNullException(nameof(scoreboardService));
            _lessonExecutionService = lessonExecutionService ?? throw new ArgumentNullException(nameof(lessonExecutionService));
            _flagService = flagService ?? throw new ArgumentNullException(nameof(flagService));
            _lessonService = lessonService ?? throw new ArgumentNullException(nameof(lessonService));
        }

        private async Task<IActionResult> RenderAsync(int? lessonId)
        {
            // Retrieve group ID
            var currentUser = await GetCurrentUserAsync();
            if(currentUser?.GroupId == null)
            {
                AddStatusMessage("Sie sind nicht eingeloggt oder keiner Gruppe zugewiesen.", StatusMessageTypes.Error);
                return await RenderViewAsync();
            }

            // Show group's most recent lesson
            if(lessonId == null)
            {
                var currentLessonExecution = await _lessonExecutionService.GetMostRecentLessonExecutionAsync(currentUser.GroupId.Value);
                if(currentLessonExecution == null)
                {
                    AddStatusMessage("Aktuell ist kein Praktikum aktiv.", StatusMessageTypes.Info);
                    return await RenderViewAsync();
                }

                lessonId = currentLessonExecution.LessonId;
            }

            // Retrieve scoreboard
            var scoreboard = await _scoreboardService.GetUserScoreboardAsync(currentUser.Id, currentUser.GroupId.Value, lessonId.Value, HttpContext.RequestAborted);
            if(scoreboard == null)
            {
                AddStatusMessage($"Die Übersicht für Praktikum #{lessonId} konnte nicht abgerufen werden.", StatusMessageTypes.Error);
                return await RenderViewAsync();
            }

            ViewData["Scoreboard"] = scoreboard;

            return await RenderViewAsync(MenuItems.Group);
        }

        [HttpGet("")]
        public Task<IActionResult> RenderLessonPageAsync(int? lessonId)
        {
            return RenderAsync(lessonId);
        }

        [HttpPost("flag")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitFlagAsync(int lessonId, string code)
        {
            // Retrieve group ID
            var currentUser = await GetCurrentUserAsync();
            if(currentUser?.GroupId == null)
            {
                AddStatusMessage("Beim Abfragen der Gruppen-ID ist ein Fehler aufgetreten.", StatusMessageTypes.Error);
                return await RenderLessonPageAsync(lessonId);
            }

            // Try to submit flag
            bool success = await _flagService.SubmitFlagAsync(currentUser.Id, lessonId, code, HttpContext.RequestAborted);
            if(success)
            {
                AddStatusMessage("Einlösen der Flag erfolgreich!", StatusMessageTypes.Success);
            }
            else
            {
                AddStatusMessage("Die Flag konnte nicht eingelöst werden.", StatusMessageTypes.Error);
            }

            return await RenderLessonPageAsync(lessonId);
        }

        [HttpGet("lessonserver")]
        public async Task<IActionResult> CallLessonServerAsync(int lessonId)
        {
            // Retrieve lesson data
            var lesson = await _lessonService.GetLessonAsync(lessonId, HttpContext.RequestAborted);
            if(lesson == null)
            {
                AddStatusMessage("Das angegebene Praktikum konnte nicht abgerufen werden.", StatusMessageTypes.Error);
                return await RenderViewAsync();
            }

            // Retrieve group ID
            var currentUser = await GetCurrentUserAsync();
            if(currentUser?.GroupId == null)
            {
                AddStatusMessage("Sie sind nicht eingeloggt oder keiner Gruppe zugewiesen.", StatusMessageTypes.Error);
                return await RenderViewAsync();
            }

            // Check whether lesson is accessible by given group
            DateTime now = DateTime.Now;
            var lessonExecution = await _lessonExecutionService.GetLessonExecutionAsync(currentUser.GroupId.Value, lessonId, HttpContext.RequestAborted);
            if(lessonExecution == null || now < lessonExecution.PreStart)
            {
                AddStatusMessage("Dieses Praktikum ist nicht aktiv.", StatusMessageTypes.Error);
                return await RenderViewAsync();
            }

            // Build authentication string
            var authData = new UserLoginRequest
            {
                UserId = currentUser.Id,
                UserDisplayName = currentUser.DisplayName,
                GroupId = currentUser.GroupId,
                GroupName = currentUser.Group?.DisplayName,
                AdminMode = false
            };
            string authString = new CryptoService(lesson.ApiCode).Encrypt(authData.Serialize());

            // Build final URL
            string url = lesson.ServerBaseUrl.TrimEnd().TrimEnd('/') + "/auth/login?code=" + authString;

            // Forward to server
            return Redirect(url);
        }
    }
}
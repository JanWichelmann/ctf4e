using System;
using System.Threading.Tasks;
using Ctf4e.Api;
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
    [Route("group")]
    [Authorize(Policy = AuthenticationStrings.PolicyIsGroupMember)]
    public class GroupController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IScoreboardService _scoreboardService;
        private readonly ILabExecutionService _labExecutionService;
        private readonly IFlagService _flagService;
        private readonly ILabService _labService;

        public GroupController(IUserService userService, IOptions<MainOptions> mainOptions, IScoreboardService scoreboardService, ILabExecutionService labExecutionService, IFlagService flagService, ILabService labService)
            : base(userService, mainOptions, "~/Views/Group.cshtml")
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _scoreboardService = scoreboardService ?? throw new ArgumentNullException(nameof(scoreboardService));
            _labExecutionService = labExecutionService ?? throw new ArgumentNullException(nameof(labExecutionService));
            _flagService = flagService ?? throw new ArgumentNullException(nameof(flagService));
            _labService = labService ?? throw new ArgumentNullException(nameof(labService));
        }

        private async Task<IActionResult> RenderAsync(int? labId)
        {
            // Retrieve group ID
            var currentUser = await GetCurrentUserAsync();
            if(currentUser?.GroupId == null)
            {
                AddStatusMessage("Fehler: Sie sind nicht eingeloggt oder keiner Gruppe zugewiesen.", StatusMessageTypes.Error);
                return await RenderViewAsync();
            }

            // Show group's most recent lab
            if(labId == null)
            {
                var currentLabExecution = await _labExecutionService.GetMostRecentLabExecutionAsync(currentUser.GroupId.Value);
                if(currentLabExecution == null)
                {
                    AddStatusMessage("Aktuell ist kein Praktikum aktiv.", StatusMessageTypes.Info);
                    return await RenderViewAsync();
                }
                labId = currentLabExecution.LabId;
            }

            // Retrieve scoreboard
            var scoreboard = await _scoreboardService.GetGroupScoreboardAsync(currentUser.Id, currentUser.GroupId.Value, labId.Value, HttpContext.RequestAborted);
            if(scoreboard == null)
            {
                AddStatusMessage($"Fehler: Das Gruppen-Scoreboard für Praktikum #{labId} konnte nicht abgerufen werden.", StatusMessageTypes.Error);
                return await RenderViewAsync();
            }
            ViewData["Scoreboard"] = scoreboard;

            return await RenderViewAsync(MenuItems.Group);
        }

        [HttpGet("")]
        public Task<IActionResult> RenderLabPageAsync(int? labId)
        {
            return RenderAsync(labId);
        }

        [HttpPost("flag")]
        public async Task<IActionResult> SubmitFlagAsync(int labId, string flagCode)
        {
            // Retrieve group ID
            var currentUser = await GetCurrentUserAsync();
            if(currentUser?.GroupId == null)
            {
                return Unauthorized("Could not retrieve group ID");
            }

            // Try to submit flag
            // TODO Do not hardcode CTF{}
            flagCode ??= "";
            bool success = await _flagService.SubmitFlagAsync(currentUser.Id, labId, $"CTF{{{ flagCode.Trim() }}}", HttpContext.RequestAborted);
            return Ok(new { success });
        }

        [HttpGet("labserver")]
        public async Task<IActionResult> CallLabServerAsync(int labId)
        {
            // Retrieve lab data
            var lab = await _labService.GetLabAsync(labId, HttpContext.RequestAborted);
            if(lab == null)
            {
                AddStatusMessage("Fehler: Das angegebene Praktikum konnte nicht abgerufen werden.", StatusMessageTypes.Error);
                return await RenderViewAsync();
            }

            // Retrieve group ID
            var currentUser = await GetCurrentUserAsync();
            if(currentUser?.GroupId == null)
            {
                AddStatusMessage("Fehler: Sie sind nicht eingeloggt oder keiner Gruppe zugewiesen.", StatusMessageTypes.Error);
                return await RenderViewAsync();
            }

            // Check whether lab is accessible by given group
            DateTime now = DateTime.Now;
            var labExecution = await _labExecutionService.GetLabExecutionAsync(currentUser.GroupId.Value, labId, HttpContext.RequestAborted);
            if(labExecution == null || now < labExecution.PreStart)
            {
                AddStatusMessage("Fehler: Dieses Praktikum ist nicht aktiv.", StatusMessageTypes.Error);
                return await RenderViewAsync();
            }

            // Build authentication string
            var authData = new UserLoginRequest()
            {
                UserId = currentUser.Id,
                UserDisplayName = currentUser.DisplayName,
                GroupId = currentUser.GroupId,
                GroupDisplayName = currentUser.Group?.DisplayName,
                AdminMode = false
            };
            string authString = new CryptoService(lab.ApiCode).Encrypt(authData.Serialize());

            // Build final URL
            string url = lab.ServerBaseUrl.TrimEnd().TrimEnd('/') + "/auth/login?code=" + authString;

            // Forward to server
            return Redirect(url);
        }
    }
}

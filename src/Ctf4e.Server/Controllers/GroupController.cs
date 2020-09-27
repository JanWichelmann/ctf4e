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
    [Route("group")]
    [Authorize(Policy = AuthenticationStrings.PolicyIsGroupMember)]
    public class GroupController : ControllerBase
    {
        private readonly IScoreboardService _scoreboardService;
        private readonly ILabExecutionService _labExecutionService;
        private readonly IFlagService _flagService;
        private readonly ILabService _labService;
        private readonly IConfigurationService _configurationService;

        public GroupController(IUserService userService, IOptions<MainOptions> mainOptions, IScoreboardService scoreboardService, ILabExecutionService labExecutionService, IFlagService flagService, ILabService labService, IConfigurationService configurationService)
            : base("~/Views/Group.cshtml", userService, mainOptions)
        {
            _scoreboardService = scoreboardService ?? throw new ArgumentNullException(nameof(scoreboardService));
            _labExecutionService = labExecutionService ?? throw new ArgumentNullException(nameof(labExecutionService));
            _flagService = flagService ?? throw new ArgumentNullException(nameof(flagService));
            _labService = labService ?? throw new ArgumentNullException(nameof(labService));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
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

            // Add prefix/suffix to flag code, if necessary
            string flagPrefix = await _configurationService.GetFlagPrefixAsync(HttpContext.RequestAborted);
            string flagSuffix = await _configurationService.GetFlagSuffixAsync(HttpContext.RequestAborted);
            flagCode ??= "";
            string fullFlagString = flagCode;
            if(!flagCode.StartsWith(flagPrefix))
                fullFlagString = flagPrefix + flagCode.Trim() + flagSuffix;

            // Try to submit flag
            bool success = await _flagService.SubmitFlagAsync(currentUser.Id, labId, fullFlagString, HttpContext.RequestAborted);
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
                GroupName = currentUser.Group?.DisplayName,
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
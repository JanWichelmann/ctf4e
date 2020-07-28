using System;
using System.Linq;
using System.Threading.Tasks;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Options;
using Ctf4e.Server.Services;
using Ctf4e.Server.ViewModels;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StackExchange.Profiling;

namespace Ctf4e.Server.Controllers
{
    [Route("")]
    [Route("scoreboard")]
    [Authorize]
    public class ScoreboardController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IScoreboardService _scoreboardService;
        private readonly ILabService _labService;
        private readonly IScoreboardCacheService _scoreboardCacheService;
        private readonly ILabExecutionService _labExecutionService;

        public ScoreboardController(IUserService userService, IOptions<MainOptions> mainOptions, IScoreboardService scoreboardService, ILabService labService, IScoreboardCacheService scoreboardCacheService, ILabExecutionService labExecutionService)
            : base(userService, mainOptions, "~/Views/Scoreboard.cshtml")
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _scoreboardService = scoreboardService ?? throw new ArgumentNullException(nameof(scoreboardService));
            _labService = labService ?? throw new ArgumentNullException(nameof(labService));
            _scoreboardCacheService = scoreboardCacheService ?? throw new ArgumentNullException(nameof(scoreboardCacheService));
            _labExecutionService = labExecutionService ?? throw new ArgumentNullException(nameof(labExecutionService));
        }

        private async Task<IActionResult> RenderAsync(ViewType viewType)
        {
            ViewData["Labs"] = await _labService.GetLabsAsync().ToListAsync(HttpContext.RequestAborted);

            ViewData["ViewType"] = viewType;
            return await RenderViewAsync(MenuItems.Scoreboard);
        }

        [HttpGet("")]
        public async Task<IActionResult> RenderScoreboardAsync(int? labId, int reload = 0, bool showAllEntries = false)
        {
            // TODO allow filtering by slots (variable is already present in scoreboard entries)

            using(MiniProfiler.Current.Step("Retrieve scoreboard"))
            {
                Scoreboard scoreboard;
                if(labId == null)
                {
                    scoreboard = await _scoreboardService.GetFullScoreboardAsync(HttpContext.RequestAborted);
                }
                else
                {
                    scoreboard = await _scoreboardService.GetLabScoreboardAsync(labId ?? 0, HttpContext.RequestAborted);
                    if(scoreboard == null)
                    {
                        AddStatusMessage("Dieses Praktikum existiert nicht oder enthält keine Aufgaben.", StatusMessageTypes.Warning);
                        return await RenderAsync(ViewType.Blank);
                    }
                }

                ViewData["Scoreboard"] = scoreboard;
            }
            
            var currentUser = await GetCurrentUserAsync();
            ViewData["ShowAllEntries"] = showAllEntries && (currentUser.IsAdmin || currentUser.IsTutor);

            if(reload > 0)
                Response.Headers.Add("Refresh", reload.ToString());

            return await RenderAsync(ViewType.Scoreboard);
        }

        [HttpPost("reset")]
        [Authorize(Policy = AuthenticationStrings.PolicyIsAdmin)]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> InvalidateScoreboardCacheAsync()
        {
            _scoreboardCacheService.InvalidateAll();

            AddStatusMessage("Löschen des Scoreboard-Caches erfolgreich.", StatusMessageTypes.Success);
            return RenderAsync(ViewType.Blank);
        }

        public enum ViewType
        {
            Blank,
            Scoreboard
        }
    }
}
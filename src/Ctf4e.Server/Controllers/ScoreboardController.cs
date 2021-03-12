﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Services;
using Ctf4e.Server.ViewModels;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using StackExchange.Profiling;

namespace Ctf4e.Server.Controllers
{
    [Route("")]
    [Route("scoreboard")]
    [Authorize]
    public class ScoreboardController : ControllerBase
    {
        private readonly IStringLocalizer<ScoreboardController> _localizer;
        private readonly IScoreboardService _scoreboardService;
        private readonly ILabService _labService;

        public ScoreboardController(IUserService userService, IStringLocalizer<ScoreboardController> localizer, IScoreboardService scoreboardService, ILabService labService)
            : base("~/Views/Scoreboard.cshtml", userService)
        {
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            _scoreboardService = scoreboardService ?? throw new ArgumentNullException(nameof(scoreboardService));
            _labService = labService ?? throw new ArgumentNullException(nameof(labService));
        }

        private async Task<IActionResult> RenderAsync(ViewType viewType)
        {
            ViewData["Labs"] = await _labService.GetLabsAsync().ToListAsync(HttpContext.RequestAborted);

            ViewData["ViewType"] = viewType;
            return await RenderViewAsync(MenuItems.Scoreboard);
        }

        [HttpGet("")]
        public async Task<IActionResult> RenderScoreboardAsync(int? labId, int reload = 0, bool showAllEntries = false, bool resetCache = false)
        {
            // TODO allow filtering by slots (variable is already present in scoreboard entries)

            using(MiniProfiler.Current.Step("Retrieve scoreboard"))
            {
                Scoreboard scoreboard;
                if(labId == null)
                {
                    scoreboard = await _scoreboardService.GetFullScoreboardAsync(HttpContext.RequestAborted, resetCache);
                }
                else
                {
                    scoreboard = await _scoreboardService.GetLabScoreboardAsync(labId ?? 0, HttpContext.RequestAborted, resetCache);
                    if(scoreboard == null)
                    {
                        AddStatusMessage(_localizer["RenderScoreboardAsync:EmptyScoreboard"], StatusMessageTypes.Warning);
                        return await RenderAsync(ViewType.Blank);
                    }
                }

                ViewData["Scoreboard"] = scoreboard;
            }

            var currentUser = await GetCurrentUserAsync();
            ViewData["ShowAllEntries"] = showAllEntries && (currentUser.IsAdmin || currentUser.IsTutor);
            ViewData["ResetCache"] = resetCache && currentUser.IsAdmin;

            if(reload > 0)
                Response.Headers.Add("Refresh", reload.ToString());

            return await RenderAsync(ViewType.Scoreboard);
        }

        public enum ViewType
        {
            Blank,
            Scoreboard
        }
    }
}
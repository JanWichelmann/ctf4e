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
        private readonly IScoreboardService _scoreboardService;
        private readonly ILessonService _lessonService;

        public ScoreboardController(IUserService userService, IOptions<MainOptions> mainOptions, IScoreboardService scoreboardService, ILessonService lessonService)
            : base("~/Views/Scoreboard.cshtml", userService, mainOptions)
        {
            _scoreboardService = scoreboardService ?? throw new ArgumentNullException(nameof(scoreboardService));
            _lessonService = lessonService ?? throw new ArgumentNullException(nameof(lessonService));
        }

        private async Task<IActionResult> RenderAsync(ViewType viewType)
        {
            ViewData["Lessons"] = await _lessonService.GetLessonsAsync().ToListAsync(HttpContext.RequestAborted);

            ViewData["ViewType"] = viewType;
            return await RenderViewAsync(MenuItems.Scoreboard);
        }

        [HttpGet("")]
        public async Task<IActionResult> RenderScoreboardAsync(int? lessonId, int reload = 0, bool showAllEntries = false, bool resetCache = false)
        {
            // TODO allow filtering by slots (variable is already present in scoreboard entries)

            using(MiniProfiler.Current.Step("Retrieve scoreboard"))
            {
                Scoreboard scoreboard;
                if(lessonId == null)
                {
                    scoreboard = await _scoreboardService.GetFullScoreboardAsync(HttpContext.RequestAborted, resetCache);
                }
                else
                {
                    scoreboard = await _scoreboardService.GetLessonScoreboardAsync(lessonId ?? 0, HttpContext.RequestAborted, resetCache);
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
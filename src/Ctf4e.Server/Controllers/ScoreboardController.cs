using System.Threading.Tasks;
using Ctf4e.Server.Authorization;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Services;
using Ctf4e.Server.ViewModels;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Profiling;

namespace Ctf4e.Server.Controllers;

[Route("")]
[Route("scoreboard")]
[Authorize]
public class ScoreboardController(IUserService userService, IScoreboardService scoreboardService)
    : ControllerBase<ScoreboardController>(userService)
{
    protected override MenuItems ActiveMenuItem => MenuItems.Scoreboard;

    private async Task<IActionResult> RenderAsync(ViewType viewType, string viewPath)
    {
        var labService = HttpContext.RequestServices.GetRequiredService<ILabService>();
        var slotService = HttpContext.RequestServices.GetRequiredService<ISlotService>();

        ViewData["Labs"] = await labService.GetLabsAsync(HttpContext.RequestAborted);
        ViewData["Slots"] = await slotService.GetSlotsAsync(HttpContext.RequestAborted);

        ViewData["ViewType"] = viewType;
        return await RenderViewAsync(viewPath);
    }

    [HttpGet("")]
    public async Task<IActionResult> RenderScoreboardAsync(int? labId, int? slotId, int reload = 0, bool showAllEntries = false, bool resetCache = false)
    {
        using(MiniProfiler.Current.Step("Retrieve scoreboard"))
        {
            Scoreboard scoreboard;
            if(labId == null)
            {
                scoreboard = await scoreboardService.GetFullScoreboardAsync(slotId, HttpContext.RequestAborted, resetCache);
            }
            else
            {
                scoreboard = await scoreboardService.GetLabScoreboardAsync(labId.Value, slotId, HttpContext.RequestAborted, resetCache);
                if(scoreboard == null)
                {
                    AddStatusMessage(StatusMessageType.Info, Localizer["RenderScoreboardAsync:EmptyScoreboard"]);
                    return await RenderAsync(ViewType.Blank, "~/Views/Scoreboard.cshtml");
                }
            }

            if(scoreboard.Entries.Count == 0)
            {
                AddStatusMessage(StatusMessageType.Info, Localizer["RenderScoreboardAsync:EmptyScoreboard"]);
                return await RenderAsync(ViewType.Blank, "~/Views/Scoreboard.cshtml");
            }

            ViewData["Scoreboard"] = scoreboard;
        }

        var currentUser = await GetCurrentUserAsync();
        ViewData["ShowAllEntries"] = showAllEntries && currentUser.Privileges.HasPrivileges(UserPrivileges.ViewAdminScoreboard);
        ViewData["ResetCache"] = resetCache && currentUser.Privileges.HasPrivileges(UserPrivileges.Admin);

        if(reload > 0 && currentUser.Privileges.HasPrivileges(UserPrivileges.ViewAdminScoreboard))
            Response.Headers["Refresh"] = reload.ToString();

        return await RenderAsync(ViewType.Scoreboard, "~/Views/Scoreboard.cshtml");
    }

    public enum ViewType
    {
        Blank,
        Scoreboard
    }
}
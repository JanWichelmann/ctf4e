using System;
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

    private async Task<IActionResult> RenderScoreboardAsync(Scoreboard scoreboard)
    {
        var labService = HttpContext.RequestServices.GetRequiredService<ILabService>();
        var slotService = HttpContext.RequestServices.GetRequiredService<ISlotService>();

        ViewData["Labs"] = await labService.GetLabsAsync(HttpContext.RequestAborted);
        ViewData["Slots"] = await slotService.GetSlotsAsync(HttpContext.RequestAborted);
        
        return await RenderViewAsync("~/Views/Scoreboard/Index.cshtml", scoreboard);
    }

    private Task<IActionResult> RenderBlankPageAsync()
    {
       return RenderViewAsync("~/Views/Scoreboard/Empty.cshtml"); 
    }

    [HttpGet("")]
    public async Task<IActionResult> RenderScoreboardAsync(int? labId, int? slotId, int reload = 0)
    {
        var configurationService = HttpContext.RequestServices.GetRequiredService<IConfigurationService>();
        if(!await configurationService.EnableScoreboard.GetAsync(HttpContext.RequestAborted))
            return NotFound(); 
        
        var currentUser = await GetCurrentUserAsync();
        
        // Extract and pass view flags
        Request.Cookies.TryGetValue("ScoreboardViewFlags", out var viewFlagsCookie);
        if(!int.TryParse(viewFlagsCookie, out int viewFlagsInt))
            viewFlagsInt = 0;
        
        var viewFlags = (ViewFlags)viewFlagsInt;
        if(!currentUser.Privileges.HasPrivileges(UserPrivileges.ViewAdminScoreboard))
            viewFlags &= ~(ViewFlags.ShowAllEntries | ViewFlags.BypassCache);
        
        ViewData["ViewFlags"] = viewFlags;
        
        bool bypassCache = (viewFlags & ViewFlags.BypassCache) != 0;
        
        Scoreboard scoreboard;
        using(MiniProfiler.Current.Step("Retrieve scoreboard"))
        {
            if(labId == null)
            {
                scoreboard = await scoreboardService.GetFullScoreboardAsync(slotId, HttpContext.RequestAborted, bypassCache);
            }
            else
            {
                scoreboard = await scoreboardService.GetLabScoreboardAsync(labId.Value, slotId, HttpContext.RequestAborted, bypassCache);
                if(scoreboard == null)
                {
                    AddStatusMessage(StatusMessageType.Info, Localizer["RenderScoreboardAsync:EmptyScoreboard"]);
                    return await RenderBlankPageAsync();
                }
            }

            if(scoreboard.Entries.Count == 0)
            {
                AddStatusMessage(StatusMessageType.Info, Localizer["RenderScoreboardAsync:EmptyScoreboard"]);
                return await RenderBlankPageAsync();
            }
        }

        if(reload > 0 && currentUser.Privileges.HasPrivileges(UserPrivileges.ViewAdminScoreboard))
        {
            Response.Headers["Refresh"] = reload.ToString();
            ViewData["AutoReload"] = reload;
        }

        return await RenderScoreboardAsync(scoreboard);
    }
    
    [Flags]
    public enum ViewFlags
    {
        ShowAllEntries = 1,
        BypassCache = 2
    }
}
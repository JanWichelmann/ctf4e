using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Ctf4e.Server.Attributes;
using Ctf4e.Server.Authorization;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Services;
using Ctf4e.Server.ViewModels;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ctf4e.Server.Controllers;

[Route("admin/config")]
[AnyUserPrivilege(UserPrivileges.EditConfiguration)]
public class AdminConfigurationController(IUserService userService, IConfigurationService configurationService)
    : ControllerBase<AdminConfigurationController>(userService)
{
    protected override MenuItems ActiveMenuItem => MenuItems.AdminConfiguration;

    private async Task<IActionResult> RenderAsync(AdminConfigurationData configurationData)
    {
        var config = configurationData ?? new AdminConfigurationData
        {
            FlagHalfPointsSubmissionCount = await configurationService.GetFlagHalfPointsSubmissionCountAsync(HttpContext.RequestAborted),
            FlagMinimumPointsDivisor = await configurationService.GetFlagMinimumPointsDivisorAsync(HttpContext.RequestAborted),
            ScoreboardEntryCount = await configurationService.GetScoreboardEntryCountAsync(HttpContext.RequestAborted),
            ScoreboardCachedSeconds = await configurationService.GetScoreboardCachedSecondsAsync(HttpContext.RequestAborted),
            PassAsGroup = await configurationService.GetPassAsGroupAsync(HttpContext.RequestAborted),
            PageTitle = await configurationService.GetPageTitleAsync(HttpContext.RequestAborted),
            NavbarTitle = await configurationService.GetNavbarTitleAsync(HttpContext.RequestAborted),
            GroupSizeMin = await configurationService.GetGroupSizeMinAsync(HttpContext.RequestAborted),
            GroupSizeMax = await configurationService.GetGroupSizeMaxAsync(HttpContext.RequestAborted),
            GroupSelectionPageText = await configurationService.GetGroupSelectionPageTextAsync(HttpContext.RequestAborted)
        };

        var groupService = HttpContext.RequestServices.GetRequiredService<IGroupService>();
        ViewData["GroupCount"] = await groupService.GetGroupsCountAsync(HttpContext.RequestAborted);
        
        // Pass build version
        string buildVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyBuildVersionAttribute>()
            .FirstOrDefault()?.Version;
        if(string.IsNullOrWhiteSpace(buildVersion))
            buildVersion = "DEV";
        ViewData["BuildVersion"] = buildVersion;

        return await RenderViewAsync("~/Views/Admin/Config/Index.cshtml", config);
    }

    [HttpGet]
    public Task<IActionResult> RenderAsync()
    {
        return RenderAsync(null);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateConfigAsync(AdminConfigurationData configurationData)
    {
        if(!ModelState.IsValid)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["UpdateConfigAsync:InvalidInput"]);
            return await RenderAsync(configurationData);
        }

        // Update configuration
        try
        {
            if(configurationData.FlagHalfPointsSubmissionCount <= 1)
                configurationData.FlagHalfPointsSubmissionCount = 2;
            await configurationService.SetFlagHalfPointsSubmissionCountAsync(configurationData.FlagHalfPointsSubmissionCount, HttpContext.RequestAborted);

            if(configurationData.FlagMinimumPointsDivisor <= 1)
                configurationData.FlagMinimumPointsDivisor = 2;
            await configurationService.SetFlagMinimumPointsDivisorAsync(configurationData.FlagMinimumPointsDivisor, HttpContext.RequestAborted);

            if(configurationData.ScoreboardEntryCount < 3)
                configurationData.ScoreboardEntryCount = 3;
            await configurationService.SetScoreboardEntryCountAsync(configurationData.ScoreboardEntryCount, HttpContext.RequestAborted);

            if(configurationData.ScoreboardCachedSeconds < 0)
                configurationData.ScoreboardCachedSeconds = 0;
            await configurationService.SetScoreboardCachedSecondsAsync(configurationData.ScoreboardCachedSeconds, HttpContext.RequestAborted);

            await configurationService.SetPassAsGroupAsync(configurationData.PassAsGroup, HttpContext.RequestAborted);

            await configurationService.SetPageTitleAsync(configurationData.PageTitle, HttpContext.RequestAborted);
            await configurationService.SetNavbarTitleAsync(configurationData.NavbarTitle, HttpContext.RequestAborted);

            if(configurationData.GroupSizeMin <= 0)
                configurationData.GroupSizeMin = 1;
            if(configurationData.GroupSizeMin > configurationData.GroupSizeMax)
                configurationData.GroupSizeMax = configurationData.GroupSizeMin + 1;
            await configurationService.SetGroupSizeMinAsync(configurationData.GroupSizeMin, HttpContext.RequestAborted);
            await configurationService.SetGroupSizeMaxAsync(configurationData.GroupSizeMax, HttpContext.RequestAborted);

            await configurationService.SetGroupSelectionPageTextAsync(configurationData.GroupSelectionPageText, HttpContext.RequestAborted);

            PostStatusMessage = new(StatusMessageType.Success, Localizer["UpdateConfigAsync:Success"]) { AutoHide = true };
            return RedirectToAction("Render");
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Update configuration");
            AddStatusMessage(StatusMessageType.Error, Localizer["UpdateConfigAsync:UnknownError"]);
            return await RenderAsync(configurationData);
        }
    }
}
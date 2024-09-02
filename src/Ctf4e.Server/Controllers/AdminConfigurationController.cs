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
            FlagHalfPointsSubmissionCount = await configurationService.FlagHalfPointsSubmissionCount.GetAsync(HttpContext.RequestAborted),
            FlagMinimumPointsDivisor = await configurationService.FlagMinimumPointsDivisor.GetAsync(HttpContext.RequestAborted),
            ScoreboardEntryCount = await configurationService.ScoreboardEntryCount.GetAsync(HttpContext.RequestAborted),
            ScoreboardCachedSeconds = await configurationService.ScoreboardCachedSeconds.GetAsync(HttpContext.RequestAborted),
            PassAsGroup = await configurationService.PassAsGroup.GetAsync(HttpContext.RequestAborted),
            ShowGroupMemberSubmissions = await configurationService.ShowGroupMemberSubmissions.GetAsync(HttpContext.RequestAborted),
            EnableScoreboard = await configurationService.EnableScoreboard.GetAsync(HttpContext.RequestAborted),
            EnableFlags = await configurationService.EnableFlags.GetAsync(HttpContext.RequestAborted),
            PageTitle = await configurationService.PageTitle.GetAsync(HttpContext.RequestAborted),
            NavbarTitle = await configurationService.NavbarTitle.GetAsync(HttpContext.RequestAborted),
            GroupSizeMin = await configurationService.GroupSizeMin.GetAsync(HttpContext.RequestAborted),
            GroupSizeMax = await configurationService.GroupSizeMax.GetAsync(HttpContext.RequestAborted),
            GroupSelectionPageText = await configurationService.GroupSelectionPageText.GetAsync(HttpContext.RequestAborted)
        };

        var groupService = HttpContext.RequestServices.GetRequiredService<IGroupService>();
        ViewData["GroupCount"] = await groupService.GetGroupsCountAsync(HttpContext.RequestAborted);
        
        // Pass build version
        string buildVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyBuildVersionAttribute>()
            .FirstOrDefault()?.Version;
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
            await configurationService.FlagHalfPointsSubmissionCount.SetAsync(configurationData.FlagHalfPointsSubmissionCount, HttpContext.RequestAborted);

            if(configurationData.FlagMinimumPointsDivisor <= 1)
                configurationData.FlagMinimumPointsDivisor = 2;
            await configurationService.FlagMinimumPointsDivisor.SetAsync(configurationData.FlagMinimumPointsDivisor, HttpContext.RequestAborted);

            if(configurationData.ScoreboardEntryCount < 3)
                configurationData.ScoreboardEntryCount = 3;
            await configurationService.ScoreboardEntryCount.SetAsync(configurationData.ScoreboardEntryCount, HttpContext.RequestAborted);

            if(configurationData.ScoreboardCachedSeconds < 0)
                configurationData.ScoreboardCachedSeconds = 0;
            await configurationService.ScoreboardCachedSeconds.SetAsync(configurationData.ScoreboardCachedSeconds, HttpContext.RequestAborted);
            
            await configurationService.PassAsGroup.SetAsync(configurationData.PassAsGroup, HttpContext.RequestAborted);
            await configurationService.ShowGroupMemberSubmissions.SetAsync(configurationData.ShowGroupMemberSubmissions, HttpContext.RequestAborted);
            await configurationService.EnableScoreboard.SetAsync(configurationData.EnableScoreboard, HttpContext.RequestAborted);
            await configurationService.EnableFlags.SetAsync(configurationData.EnableFlags, HttpContext.RequestAborted);

            await configurationService.PageTitle.SetAsync(configurationData.PageTitle, HttpContext.RequestAborted);
            await configurationService.NavbarTitle.SetAsync(configurationData.NavbarTitle, HttpContext.RequestAborted);

            if(configurationData.GroupSizeMin <= 0)
                configurationData.GroupSizeMin = 1;
            if(configurationData.GroupSizeMin > configurationData.GroupSizeMax)
                configurationData.GroupSizeMax = configurationData.GroupSizeMin;
            await configurationService.GroupSizeMin.SetAsync(configurationData.GroupSizeMin, HttpContext.RequestAborted);
            await configurationService.GroupSizeMax.SetAsync(configurationData.GroupSizeMax, HttpContext.RequestAborted);

            await configurationService.GroupSelectionPageText.SetAsync(configurationData.GroupSelectionPageText, HttpContext.RequestAborted);

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
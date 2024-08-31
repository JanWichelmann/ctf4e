using System;
using System.Text;
using System.Threading.Tasks;
using Ctf4e.Api.Models;
using Ctf4e.Api.Services;
using Ctf4e.Server.Authorization;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Services;
using Ctf4e.Server.Services.Sync;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ctf4e.Server.Controllers;

[Route("admin/scoreboard")]
[AnyUserPrivilege(UserPrivileges.ViewAdminScoreboard)]
public partial class AdminScoreboardController(
    IUserService userService,
    IAdminScoreboardService adminScoreboardService,
    ILabService labService,
    ISlotService slotService)
    : ControllerBase<AdminScoreboardController>(userService)
{
    protected override MenuItems ActiveMenuItem => MenuItems.AdminScoreboard;

    private async Task<IActionResult> RenderAsync(string viewPath, object model)
    {
        ViewData["Labs"] = await labService.GetSelectLabListAsync(HttpContext.RequestAborted);
        ViewData["Slots"] = await slotService.GetSelectSlotListAsync(HttpContext.RequestAborted);

        return await RenderViewAsync(viewPath, model);
    }

    private async Task<(int LabId, int SlotId)> GetRecentLabAndSlotAsync()
    {
        var labExecutionService = HttpContext.RequestServices.GetRequiredService<ILabExecutionService>();

        // Show the most recently executed lab and slot as default
        var labExecution = await labExecutionService.FindMostRecentLabExecutionAsync(HttpContext.RequestAborted);
        if(labExecution != null)
            return (labExecution.LabId, labExecution.Group?.SlotId ?? 0);
        return (0, 0);
    }

    private async Task<IActionResult> ShowErrorViewAsync(string message)
    {
        AddStatusMessage(StatusMessageType.Error, message);
        return await RenderAsync("~/Views/Admin/Scoreboard/Empty.cshtml", null);
    }

    [HttpGet]
    [HttpGet("stats")]
    public async Task<IActionResult> ShowStatisticsDashboardAsync(int? labId, int? slotId)
    {
        if(labId == null || slotId == null)
            (labId, slotId) = await GetRecentLabAndSlotAsync();

        if(!await labService.LabExistsAsync(labId.Value, HttpContext.RequestAborted))
            return await ShowErrorViewAsync(Localizer["LabNotFound"]);
        if(!await slotService.SlotExistsAsync(slotId.Value, HttpContext.RequestAborted))
            return await ShowErrorViewAsync(Localizer["SlotNotFound"]);

        var statistics = await adminScoreboardService.GetStatisticsAsync(labId.Value, slotId.Value, HttpContext.RequestAborted);

        return await RenderAsync("~/Views/Admin/Scoreboard/Index.cshtml", statistics);
    }

    [HttpGet("groups")]
    public async Task<IActionResult> ShowGroupsOverviewAsync(int? labId, int? slotId)
    {
        if(labId == null || slotId == null)
            (labId, slotId) = await GetRecentLabAndSlotAsync();

        if(!await labService.LabExistsAsync(labId.Value, HttpContext.RequestAborted))
            return await ShowErrorViewAsync(Localizer["LabNotFound"]);
        if(!await slotService.SlotExistsAsync(slotId.Value, HttpContext.RequestAborted))
            return await ShowErrorViewAsync(Localizer["SlotNotFound"]);

        var overview = await adminScoreboardService.GetOverviewAsync(labId.Value, slotId.Value, HttpContext.RequestAborted);

        return await RenderAsync("~/Views/Admin/Scoreboard/Groups.cshtml", overview);
    }

    [HttpGet("users")]
    public async Task<IActionResult> ShowUsersOverviewAsync(int? labId, int? slotId)
    {
        if(labId == null || slotId == null)
            (labId, slotId) = await GetRecentLabAndSlotAsync();

        if(!await labService.LabExistsAsync(labId.Value, HttpContext.RequestAborted))
            return await ShowErrorViewAsync(Localizer["LabNotFound"]);
        if(!await slotService.SlotExistsAsync(slotId.Value, HttpContext.RequestAborted))
            return await ShowErrorViewAsync(Localizer["SlotNotFound"]);

        var overview = await adminScoreboardService.GetOverviewAsync(labId.Value, slotId.Value, HttpContext.RequestAborted);

        return await RenderAsync("~/Views/Admin/Scoreboard/Users.cshtml", overview);
    }

    [HttpGet("group/{groupId}")]
    public async Task<IActionResult> ShowGroupDashboardAsync(int groupId, int labId, [FromServices] IGroupService groupService)
    {
        if(!await groupService.GroupExistsAsync(groupId, HttpContext.RequestAborted))
            return await ShowErrorViewAsync(Localizer["GroupNotFound"]);
        if(!await labService.LabExistsAsync(labId, HttpContext.RequestAborted))
            return await ShowErrorViewAsync(Localizer["LabNotFound"]);

        var details = await adminScoreboardService.GetDetailsAsync(labId, groupId, null, HttpContext.RequestAborted);

        return await RenderAsync("~/Views/Admin/Scoreboard/GroupDashboard.cshtml", details);
    }

    [HttpGet("user/{userId}")]
    public async Task<IActionResult> ShowUserDashboardAsync(int userId, int labId)
    {
        if(!await userService.UserExistsAsync(userId, HttpContext.RequestAborted))
            return await ShowErrorViewAsync(Localizer["UserNotFound"]);
        if(!await labService.LabExistsAsync(labId, HttpContext.RequestAborted))
            return await ShowErrorViewAsync(Localizer["LabNotFound"]);

        var details = await adminScoreboardService.GetDetailsAsync(labId, null, userId, HttpContext.RequestAborted);

        return await RenderAsync("~/Views/Admin/Scoreboard/UserDashboard.cshtml", details);
    }

    [HttpGet("export")]
    public async Task<IActionResult> ShowExportPageAsync()
    {
        return await RenderAsync("~/Views/Admin/Scoreboard/Export.cshtml", null);
    }

    [HttpGet("labserver")]
    [AnyUserPrivilege(UserPrivileges.LoginAsLabServerAdmin)]
    public async Task<IActionResult> CallLabServerAsync(int labId, int userId, [FromServices] IGroupService groupService)
    {
        // Retrieve lab data
        var lab = await labService.FindLabByIdAsync(labId, HttpContext.RequestAborted);
        if(lab == null)
        {
            PostStatusMessage = new(StatusMessageType.Error, Localizer["CallLabServerAsync:NotFound"]);
            return RedirectToAction("ShowStatisticsDashboard");
        }

        // Build authentication string
        var user = await userService.FindUserByIdAsync(userId, HttpContext.RequestAborted);
        var group = user.GroupId == null ? null : await groupService.FindGroupByIdAsync(user.GroupId.Value, HttpContext.RequestAborted);
        var authData = new UserLoginRequest
        {
            UserId = userId,
            UserDisplayName = user.DisplayName,
            GroupId = group?.Id,
            GroupName = group?.DisplayName,
            AdminMode = true
        };
        string authString = new CryptoService(lab.ApiCode).Encrypt(authData.Serialize());

        // Build final URL
        string url = lab.ServerBaseUrl.TrimEnd().TrimEnd('/') + "/auth/login?code=" + authString;

        // Forward to server
        return Redirect(url);
    }

    [HttpPost("sync/moodle")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.TransferResults)]
    public async Task<IActionResult> UploadToMoodleAsync([FromServices] IMoodleService moodleService)
    {
        try
        {
            await moodleService.UploadStateToMoodleAsync(HttpContext.RequestAborted);

            PostStatusMessage = new StatusMessage(StatusMessageType.Success, Localizer["UploadToMoodleAsync:Success"]);
        }
        catch(InvalidOperationException ex)
        {
            GetLogger().LogError(ex, "Upload to Moodle");
            PostStatusMessage = new StatusMessage(StatusMessageType.Error, Localizer["UploadToMoodleAsync:UnknownError"]);
        }

        return RedirectToAction("ShowStatisticsDashboard");
    }

    [HttpGet("sync/csv")]
    [AnyUserPrivilege(UserPrivileges.TransferResults)]
    public async Task<IActionResult> DownloadAsCsvAsync([FromServices] ICsvService csvService)
    {
        try
        {
            string csv = await csvService.GetLabStatesAsync(HttpContext.RequestAborted);
            return File(Encoding.UTF8.GetBytes(csv), "text/csv", "labstates.csv");
        }
        catch(InvalidOperationException ex)
        {
            GetLogger().LogError(ex, "Download as CSV");
            AddStatusMessage(StatusMessageType.Error, Localizer["DownloadAsCsvAsync:UnknownError"]);
        }

        return await ShowStatisticsDashboardAsync(null, null);
    }
}
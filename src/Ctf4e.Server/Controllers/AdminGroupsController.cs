using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ctf4e.Server.Authorization;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Models;
using Ctf4e.Server.Services;
using Ctf4e.Server.Services.Sync;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Ctf4e.Server.Controllers;

[Route("admin/groups")]
[AnyUserPrivilege(UserPrivileges.ViewGroups)]
public class AdminGroupsController(IUserService userService, IGroupService groupService, ISlotService slotService)
    : ControllerBase<AdminGroupsController>(userService)
{
    protected override MenuItems ActiveMenuItem => MenuItems.AdminGroups;

    private Task<IActionResult> RenderAsync(ViewType viewType, string viewPath, object model)
    {
        ViewData["ViewType"] = viewType;
        return RenderViewAsync(viewPath, model);
    }

    [HttpGet]
    public async Task<IActionResult> RenderGroupListAsync()
    {
        // Pass groups
        var groups = await groupService.GetGroupsAsync(HttpContext.RequestAborted);

        return await RenderAsync(ViewType.List, "~/Views/AdminGroups.cshtml", groups);
    }

    private async Task<IActionResult> ShowEditGroupFormAsync(int? id, Group group = null)
    {
        // Retrieve by ID, if no object from a failed POST was passed
        if(id != null)
        {
            group = await groupService.FindGroupByIdAsync(id.Value, HttpContext.RequestAborted);
            if(group == null)
            {
                AddStatusMessage(StatusMessageType.Error, Localizer["ShowEditGroupFormAsync:NotFound"]);
                return await RenderGroupListAsync();
            }
        }

        if(group == null)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["ShowEditGroupFormAsync:MissingParameter"]);
            return await RenderGroupListAsync();
        }

        // Pass list of slots
        ViewData["Slots"] = await slotService.GetSlotsAsync(HttpContext.RequestAborted);

        return await RenderAsync(ViewType.Edit, "~/Views/AdminGroups.cshtml", group);
    }

    [HttpGet("edit")]
    [AnyUserPrivilege(UserPrivileges.EditGroups)]
    public Task<IActionResult> ShowEditGroupFormAsync(int id)
    {
        return ShowEditGroupFormAsync(id, null);
    }

    [HttpPost("edit")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditGroups)]
    public async Task<IActionResult> EditGroupAsync(Group groupData)
    {
        // Check input
        if(!ModelState.IsValid)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["EditGroupAsync:InvalidInput"]);
            return await ShowEditGroupFormAsync(null, groupData);
        }

        try
        {
            // Retrieve edited group from database and apply changes
            var group = await groupService.FindGroupByIdAsync(groupData.Id, HttpContext.RequestAborted);
            group.DisplayName = groupData.DisplayName;
            group.ScoreboardAnnotation = groupData.ScoreboardAnnotation;
            group.ScoreboardAnnotationHoverText = groupData.ScoreboardAnnotationHoverText;
            group.SlotId = groupData.SlotId;
            group.ShowInScoreboard = groupData.ShowInScoreboard;
            await groupService.UpdateGroupAsync(group, HttpContext.RequestAborted);

            AddStatusMessage(StatusMessageType.Success, Localizer["EditGroupAsync:Success"]);
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Edit group");
            AddStatusMessage(StatusMessageType.Error, Localizer["EditGroupAsync:UnknownError"]);
            return await ShowEditGroupFormAsync(null, groupData);
        }

        return await RenderGroupListAsync();
    }

    [HttpGet("create")]
    [AnyUserPrivilege(UserPrivileges.EditGroups)]
    public async Task<IActionResult> ShowCreateGroupFormAsync(Group group = null)
    {
        // Pass list of slots
        ViewData["Slots"] = await slotService.GetSlotsAsync(HttpContext.RequestAborted);

        return await RenderAsync(ViewType.Create, "~/Views/AdminGroups.cshtml", group);
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditGroups)]
    public async Task<IActionResult> CreateGroupAsync(Group groupData)
    {
        // Check input
        if(!ModelState.IsValid)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateGroupAsync:InvalidInput"]);
            return await ShowCreateGroupFormAsync(groupData);
        }

        try
        {
            // Create group
            var group = new Group
            {
                DisplayName = groupData.DisplayName,
                ScoreboardAnnotation = groupData.ScoreboardAnnotation,
                ScoreboardAnnotationHoverText = groupData.ScoreboardAnnotationHoverText,
                SlotId = groupData.SlotId,
                ShowInScoreboard = groupData.ShowInScoreboard
            };
            await groupService.CreateGroupAsync(group, HttpContext.RequestAborted);

            AddStatusMessage(StatusMessageType.Success, Localizer["CreateGroupAsync:Success"]);
        }
        catch(InvalidOperationException ex)
        {
            GetLogger().LogError(ex, "Create group");
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateGroupAsync:UnknownError"]);
            return await ShowCreateGroupFormAsync(groupData);
        }

        return await RenderGroupListAsync();
    }

    [HttpPost("delete")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditGroups)]
    public async Task<IActionResult> DeleteGroupAsync(int id)
    {
        // Input check
        var group = await groupService.FindGroupByIdAsync(id, HttpContext.RequestAborted);
        if(group == null)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["DeleteGroupAsync:NotFound"]);
            return await RenderGroupListAsync();
        }

        if(group.Members.Any())
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["DeleteGroupAsync:GroupNotEmpty"]);
            return await RenderGroupListAsync();
        }

        try
        {
            // Delete group
            await groupService.DeleteGroupAsync(id, HttpContext.RequestAborted);

            AddStatusMessage(StatusMessageType.Success, Localizer["DeleteGroupAsync:Success"]);
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Delete group");
            AddStatusMessage(StatusMessageType.Error, Localizer["DeleteGroupAsync:UnknownError"]);
        }

        return await RenderGroupListAsync();
    }

    [HttpGet("sync/json")]
    [AnyUserPrivilege(UserPrivileges.TransferResults)]
    public async Task<IActionResult> DownloadAsJsonAsync([FromServices] IDumpService dumpService)
    {
        try
        {
            string json = await dumpService.GetGroupDataAsync(HttpContext.RequestAborted);
            return File(Encoding.UTF8.GetBytes(json), "text/json", "groups.json");
        }
        catch(InvalidOperationException ex)
        {
            GetLogger().LogError(ex, "Download as JSON");
            AddStatusMessage(StatusMessageType.Error, Localizer["DownloadAsJsonAsync:UnknownError"]);
            return await RenderGroupListAsync();
        }
    }

    public enum ViewType
    {
        List,
        Edit,
        Create
    }
}
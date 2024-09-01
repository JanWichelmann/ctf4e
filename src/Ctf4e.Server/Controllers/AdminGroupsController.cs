using System;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using Ctf4e.Server.Authorization;
using Ctf4e.Server.Constants;
using Ctf4e.Server.InputModels;
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

    [HttpGet]
    public async Task<IActionResult> RenderGroupListAsync()
    {
        // Pass groups
        var groups = await groupService.GetGroupListAsync(HttpContext.RequestAborted);

        return await RenderViewAsync("~/Views/Admin/Groups/Index.cshtml", groups);
    }

    private async Task<IActionResult> ShowEditGroupFormAsync(AdminGroupInputModel groupInput)
    {
        // Pass list of slots
        ViewData["Slots"] = await slotService.GetSlotsAsync(HttpContext.RequestAborted);

        return await RenderViewAsync("~/Views/Admin/Groups/Edit.cshtml", groupInput);
    }

    [HttpGet("edit")]
    [AnyUserPrivilege(UserPrivileges.EditGroups)]
    public async Task<IActionResult> ShowEditGroupFormAsync(int id, [FromServices] IMapper mapper)
    {
        var group = await groupService.FindGroupByIdAsync(id, HttpContext.RequestAborted);
        if(group == null)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["ShowEditGroupFormAsync:NotFound"]);
            return await RenderGroupListAsync();
        }

        var groupInput = mapper.Map<AdminGroupInputModel>(group);
        return await ShowEditGroupFormAsync(groupInput);
    }

    [HttpPost("edit")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditGroups)]
    public async Task<IActionResult> EditGroupAsync(AdminGroupInputModel groupInput, [FromServices] IMapper mapper)
    {
        // Check input
        if(!ModelState.IsValid || groupInput.Id == null)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["EditGroupAsync:InvalidInput"]);
            return await ShowEditGroupFormAsync(groupInput);
        }

        try
        {
            // Retrieve edited group from database and apply changes
            var group = await groupService.FindGroupByIdAsync(groupInput.Id.Value, HttpContext.RequestAborted);
            if(group == null)
            {
                PostStatusMessage = new StatusMessage(StatusMessageType.Error, Localizer["EditGroupAsync:NotFound"]);
                return RedirectToAction("RenderGroupList");
            }

            mapper.Map(groupInput, group);

            await groupService.UpdateGroupAsync(group, HttpContext.RequestAborted);

            PostStatusMessage = new StatusMessage(StatusMessageType.Success, Localizer["EditGroupAsync:Success"]) { AutoHide = true };
            return RedirectToAction("RenderGroupList");
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Edit group");
            AddStatusMessage(StatusMessageType.Error, Localizer["EditGroupAsync:UnknownError"]);
            return await ShowEditGroupFormAsync(groupInput);
        }
    }

    private async Task<IActionResult> ShowCreateGroupFormAsync(AdminGroupInputModel groupInput)
    {
        // Pass list of slots
        ViewData["Slots"] = await slotService.GetSlotsAsync(HttpContext.RequestAborted);

        return await RenderViewAsync("~/Views/Admin/Groups/Create.cshtml", groupInput);
    }

    [HttpGet("create")]
    [AnyUserPrivilege(UserPrivileges.EditGroups)]
    public Task<IActionResult> ShowCreateGroupFormAsync()
        => ShowCreateGroupFormAsync(null);

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditGroups)]
    public async Task<IActionResult> CreateGroupAsync(AdminGroupInputModel groupInput, [FromServices] IMapper mapper)
    {
        // Check input
        if(!ModelState.IsValid)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateGroupAsync:InvalidInput"]);
            return await ShowCreateGroupFormAsync(groupInput);
        }

        try
        {
            // Create group
            var group = mapper.Map<Group>(groupInput);
            await groupService.CreateGroupAsync(group, HttpContext.RequestAborted);

            PostStatusMessage = new StatusMessage(StatusMessageType.Success, Localizer["CreateGroupAsync:Success"]) { AutoHide = true };
            return RedirectToAction("RenderGroupList");
        }
        catch(InvalidOperationException ex)
        {
            GetLogger().LogError(ex, "Create group");
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateGroupAsync:UnknownError"]);
            return await ShowCreateGroupFormAsync(groupInput);
        }
    }

    [HttpPost("delete")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditGroups)]
    public async Task<IActionResult> DeleteGroupAsync(int id)
    {
        try
        {
            // Input check
            var group = await groupService.FindGroupByIdAsync(id, HttpContext.RequestAborted);
            if(group == null)
            {
                PostStatusMessage = new StatusMessage(StatusMessageType.Error, Localizer["DeleteGroupAsync:NotFound"]);
                return RedirectToAction("RenderGroupList");
            }

            if(await groupService.GetGroupMemberCount(id, HttpContext.RequestAborted) != 0)
            {
                PostStatusMessage = new StatusMessage(StatusMessageType.Error, Localizer["DeleteGroupAsync:GroupNotEmpty"]);
                return RedirectToAction("RenderGroupList");
            }

            // Delete group
            await groupService.DeleteGroupAsync(id, HttpContext.RequestAborted);

            PostStatusMessage = new StatusMessage(StatusMessageType.Success, Localizer["DeleteGroupAsync:Success"]) { AutoHide = true };
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Delete group");
            PostStatusMessage = new StatusMessage(StatusMessageType.Error, Localizer["DeleteGroupAsync:UnknownError"]);
        }

        return RedirectToAction("RenderGroupList");
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
            PostStatusMessage = new StatusMessage(StatusMessageType.Error, Localizer["DownloadAsJsonAsync:UnknownError"]);
            return RedirectToAction("RenderGroupList");
        }
    }

    public static void RegisterMappings(Profile mappingProfile)
    {
        mappingProfile.CreateMap<Group, AdminGroupInputModel>();
        mappingProfile.CreateMap<AdminGroupInputModel, Group>();
    }
}
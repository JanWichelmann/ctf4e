using System;
using System.Threading.Tasks;
using Ctf4e.Server.Authorization;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Models;
using Ctf4e.Server.Services;
using Ctf4e.Server.ViewModels;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Ctf4e.Server.Controllers;

[Route("admin/executions")]
[AnyUserPrivilege(UserPrivileges.ViewLabExecutions)]
public class AdminLabExecutionsController(IUserService userService, ILabExecutionService labExecutionService, ILabService labService, IGroupService groupService, ISlotService slotService)
    : ControllerBase<AdminLabExecutionsController>(userService)
{
    protected override MenuItems ActiveMenuItem => MenuItems.AdminLabExecutions;

    private readonly IUserService _userService = userService;

    private Task<IActionResult> RenderAsync(ViewType viewType, string viewPath, object model)
    {
        ViewData["ViewType"] = viewType;
        return RenderViewAsync(viewPath, model);
    }

    [HttpGet]
    public async Task<IActionResult> RenderLabExecutionListAsync()
    {
        // Pass data
        var labExecutions = await labExecutionService.GetLabExecutionsAsync(HttpContext.RequestAborted);

        ViewData["Labs"] = await labService.GetLabsAsync(HttpContext.RequestAborted);
        ViewData["Slots"] = await slotService.GetSlotsAsync(HttpContext.RequestAborted);

        return await RenderAsync(ViewType.List, "~/Views/AdminLabExecutions.cshtml", labExecutions);
    }

    private async Task<IActionResult> ShowEditLabExecutionFormAsync(int? groupId, int? labId, AdminLabExecution labExecutionData = null)
    {
        // Retrieve by ID, if no object from a failed POST was passed
        if(groupId != null && labId != null)
        {
            labExecutionData = new AdminLabExecution
            {
                LabExecution = await labExecutionService.FindLabExecutionAsync(groupId.Value, labId.Value, HttpContext.RequestAborted)
            };
            if(labExecutionData.LabExecution == null)
            {
                AddStatusMessage(StatusMessageType.Error, Localizer["ShowEditLabExecutionFormAsync:NotFound"]);
                return await RenderLabExecutionListAsync();
            }
        }

        if(labExecutionData?.LabExecution == null)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["ShowEditLabExecutionFormAsync:MissingParameter"]);
            return await RenderLabExecutionListAsync();
        }

        return await RenderAsync(ViewType.Edit, "~/Views/AdminLabExecutions.cshtml", labExecutionData);
    }

    [HttpGet("edit")]
    [AnyUserPrivilege(UserPrivileges.EditLabExecutions)]
    public Task<IActionResult> ShowEditLabExecutionFormAsync(int groupId, int labId)
    {
        // Always show warning
        AddStatusMessage(StatusMessageType.Warning, Localizer["ShowEditLabExecutionFormAsync:Warning"]);

        return ShowEditLabExecutionFormAsync(groupId, labId, null);
    }

    [HttpPost("edit")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabExecutions)]
    public async Task<IActionResult> EditLabExecutionAsync(AdminLabExecution labExecutionData)
    {
        // Check input
        if(!ModelState.IsValid || !(labExecutionData.LabExecution.Start < labExecutionData.LabExecution.End))
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["EditLabExecutionAsync:InvalidInput"]);
            return await ShowEditLabExecutionFormAsync(null, null, labExecutionData);
        }

        try
        {
            // Retrieve edited labExecution from database and apply changes
            var labExecution = await labExecutionService.FindLabExecutionAsync(labExecutionData.LabExecution.GroupId, labExecutionData.LabExecution.LabId, HttpContext.RequestAborted);
            labExecution.Start = labExecutionData.LabExecution.Start;
            labExecution.End = labExecutionData.LabExecution.End;
            await labExecutionService.UpdateLabExecutionAsync(labExecution, HttpContext.RequestAborted);

            AddStatusMessage(StatusMessageType.Success, Localizer["EditLabExecutionAsync:Success"]);
        }
        catch(InvalidOperationException ex)
        {
            GetLogger().LogError(ex, "Edit lab execution");
            AddStatusMessage(StatusMessageType.Error, Localizer["EditLabExecutionAsync:UnknownError"]);
            return await ShowEditLabExecutionFormAsync(null, null, labExecutionData);
        }

        return await RenderLabExecutionListAsync();
    }

    [HttpGet("create/slot")]
    [AnyUserPrivilege(UserPrivileges.EditLabExecutions)]
    public async Task<IActionResult> ShowCreateLabExecutionForSlotFormAsync(AdminLabExecution labExecutionData = null)
    {
        // Pass lists
        ViewData["Labs"] = await labService.GetLabsAsync(HttpContext.RequestAborted);
        ViewData["Slots"] = await slotService.GetSlotsAsync(HttpContext.RequestAborted);

        return await RenderAsync(ViewType.CreateForSlot, "~/Views/AdminLabExecutions.cshtml", labExecutionData);
    }

    [HttpPost("create/slot")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabExecutions)]
    public async Task<IActionResult> CreateLabExecutionForSlotAsync(AdminLabExecution labExecutionData)
    {
        // Check input
        if(!ModelState.IsValid
           || !await labService.LabExistsAsync(labExecutionData.LabExecution.LabId, HttpContext.RequestAborted)
           || !await slotService.SlotExistsAsync(labExecutionData.SlotId, HttpContext.RequestAborted)
           || !(labExecutionData.LabExecution.Start < labExecutionData.LabExecution.End))
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateLabExecutionForSlotAsync:InvalidInput"]);
            return await ShowCreateLabExecutionForSlotFormAsync(labExecutionData);
        }

        try
        {
            // Start lab for each of the groups
            foreach(var group in await groupService.GetGroupsInSlotAsync(labExecutionData.SlotId, HttpContext.RequestAborted))
            {
                try
                {
                    var labExecution = new LabExecution
                    {
                        GroupId = group.Id,
                        LabId = labExecutionData.LabExecution.LabId,
                        Start = labExecutionData.LabExecution.Start,
                        End = labExecutionData.LabExecution.End
                    };
                    await labExecutionService.CreateLabExecutionAsync(labExecution, labExecutionData.OverrideExisting, HttpContext.RequestAborted);
                }
                catch(Exception ex)
                {
                    GetLogger().LogError(ex, "Create lab execution for group in slot");
                    AddStatusMessage(StatusMessageType.Warning, Localizer["CreateLabExecutionForSlotAsync:ErrorGroup", group.Id, group.DisplayName]);
                }
            }

            AddStatusMessage(StatusMessageType.Success, Localizer["CreateLabExecutionForSlotAsync:Success"]);
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Create lab execution for slot");
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateLabExecutionForSlotAsync:UnknownError"]);
            return await ShowCreateLabExecutionForSlotFormAsync(labExecutionData);
        }

        return await RenderLabExecutionListAsync();
    }

    [HttpGet("create/group")]
    [AnyUserPrivilege(UserPrivileges.EditLabExecutions)]
    public async Task<IActionResult> ShowCreateLabExecutionForGroupFormAsync(AdminLabExecution labExecutionData = null)
    {
        // Pass lists
        ViewData["Labs"] = await labService.GetLabsAsync(HttpContext.RequestAborted);
        ViewData["Groups"] = await groupService.GetGroupsAsync(HttpContext.RequestAborted);

        return await RenderAsync(ViewType.CreateForGroup, "~/Views/AdminLabExecutions.cshtml", labExecutionData);
    }

    [HttpPost("create/group")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabExecutions)]
    public async Task<IActionResult> CreateLabExecutionForGroupAsync(AdminLabExecution labExecutionData)
    {
        // Check input
        if(!ModelState.IsValid
           || !await labService.LabExistsAsync(labExecutionData.LabExecution.LabId, HttpContext.RequestAborted)
           || !await groupService.GroupExistsAsync(labExecutionData.LabExecution.GroupId, HttpContext.RequestAborted)
           || !(labExecutionData.LabExecution.Start < labExecutionData.LabExecution.End))
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateLabExecutionForGroupAsync:InvalidInput"]);
            return await ShowCreateLabExecutionForGroupFormAsync(labExecutionData);
        }

        // Start lab for group
        try
        {
            var labExecution = new LabExecution
            {
                GroupId = labExecutionData.LabExecution.GroupId,
                LabId = labExecutionData.LabExecution.LabId,
                Start = labExecutionData.LabExecution.Start,
                End = labExecutionData.LabExecution.End
            };
            await labExecutionService.CreateLabExecutionAsync(labExecution, labExecutionData.OverrideExisting, HttpContext.RequestAborted);

            AddStatusMessage(StatusMessageType.Success, Localizer["CreateLabExecutionForGroupAsync:Success"]);
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Create lab execution for group");
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateLabExecutionForGroupAsync:UnknownError"]);
            return await ShowCreateLabExecutionForGroupFormAsync(labExecutionData);
        }

        return await RenderLabExecutionListAsync();
    }

    [HttpPost("delete/group")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabExecutions)]
    public async Task<IActionResult> DeleteLabExecutionForGroupAsync(int groupId, int labId)
    {
        // Input check
        var labExecution = await labExecutionService.FindLabExecutionAsync(groupId, labId, HttpContext.RequestAborted);
        if(labExecution == null)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["DeleteLabExecutionForGroupAsync:NotFound"]);
            return await RenderLabExecutionListAsync();
        }

        try
        {
            // Delete execution
            await labExecutionService.DeleteLabExecutionAsync(groupId, labId, HttpContext.RequestAborted);

            AddStatusMessage(StatusMessageType.Success, Localizer["DeleteLabExecutionForGroupAsync:Success"]);
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Delete lab execution for group");
            AddStatusMessage(StatusMessageType.Error, Localizer["DeleteLabExecutionForGroupAsync:UnknownError"]);
        }

        return await RenderLabExecutionListAsync();
    }

    [HttpPost("delete/slot")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabExecutions)]
    public async Task<IActionResult> DeleteLabExecutionForSlotAsync(int slotId, int labId)
    {
        try
        {
            // Delete all executions for the given slot
            await labExecutionService.DeleteLabExecutionsForSlotAsync(slotId, labId, HttpContext.RequestAborted);

            AddStatusMessage(StatusMessageType.Success, Localizer["DeleteLabExecutionForSlotAsync:Success"]);
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Delete lab execution for slot");
            AddStatusMessage(StatusMessageType.Error, Localizer["DeleteLabExecutionForSlotAsync:UnknownError"]);
        }

        return await RenderLabExecutionListAsync();
    }

    public enum ViewType
    {
        List,
        Edit,
        CreateForSlot,
        CreateForGroup
    }
}
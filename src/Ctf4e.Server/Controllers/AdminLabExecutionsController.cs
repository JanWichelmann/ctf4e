using System;
using System.Threading.Tasks;
using AutoMapper;
using Ctf4e.Server.Authorization;
using Ctf4e.Server.Constants;
using Ctf4e.Server.InputModels;
using Ctf4e.Server.Models;
using Ctf4e.Server.Services;
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

    [HttpGet]
    public async Task<IActionResult> RenderLabExecutionListAsync()
    {
        // Pass data
        var labExecutions = await labExecutionService.GetLabExecutionListAsync(HttpContext.RequestAborted);

        ViewData["Labs"] = await labService.GetLabsAsync(HttpContext.RequestAborted);
        ViewData["Slots"] = await slotService.GetSlotsAsync(HttpContext.RequestAborted);
        ViewData["Groups"] = await groupService.GetGroupsAsync(HttpContext.RequestAborted);

        return await RenderViewAsync("~/Views/Admin/LabExecutions/Index.cshtml", labExecutions);
    }

    private async Task<IActionResult> ShowEditLabExecutionFormAsync(AdminLabExecutionInputModel labExecutionInput)
    {
        ViewData["Labs"] = await labService.GetLabsAsync(HttpContext.RequestAborted);
        ViewData["Groups"] = await groupService.GetGroupsAsync(HttpContext.RequestAborted);

        ViewData["EditMode"] = true;

        return await RenderViewAsync("~/Views/Admin/LabExecutions/Edit.cshtml", labExecutionInput);
    }

    [HttpGet("edit")]
    [AnyUserPrivilege(UserPrivileges.EditLabExecutions)]
    public async Task<IActionResult> ShowEditLabExecutionFormAsync(int groupId, int labId, [FromServices] IMapper mapper)
    {
        var labExecution = await labExecutionService.FindLabExecutionAsync(groupId, labId, HttpContext.RequestAborted);
        if(labExecution == null)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["ShowEditLabExecutionFormAsync:NotFound"]);
            return await RenderLabExecutionListAsync();
        }

        var labExecutionInput = mapper.Map<AdminLabExecutionInputModel>(labExecution);

        // Always show warning
        AddStatusMessage(StatusMessageType.Warning, Localizer["ShowEditLabExecutionFormAsync:Warning"]);
        return await ShowEditLabExecutionFormAsync(labExecutionInput);
    }

    [HttpPost("edit")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabExecutions)]
    public async Task<IActionResult> EditLabExecutionAsync(AdminLabExecutionInputModel labExecutionInput, [FromServices] IMapper mapper)
    {
        // Check input
        if(!ModelState.IsValid || !(labExecutionInput.Start < labExecutionInput.End))
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["EditLabExecutionAsync:InvalidInput"]);
            return await ShowEditLabExecutionFormAsync(labExecutionInput);
        }

        try
        {
            // Retrieve edited labExecution from database and apply changes
            var labExecution = await labExecutionService.FindLabExecutionAsync(labExecutionInput.GroupId, labExecutionInput.LabId, HttpContext.RequestAborted);
            if(labExecution == null)
            {
                PostStatusMessage = new StatusMessage(StatusMessageType.Error, Localizer["EditLabExecutionAsync:NotFound"]);
                return RedirectToAction("RenderLabExecutionList");
            }

            mapper.Map(labExecutionInput, labExecution);

            await labExecutionService.UpdateLabExecutionAsync(labExecution, HttpContext.RequestAborted);

            PostStatusMessage = new StatusMessage(StatusMessageType.Success, Localizer["EditLabExecutionAsync:Success"]) { AutoHide = true };
            return RedirectToAction("RenderLabExecutionList");
        }
        catch(InvalidOperationException ex)
        {
            GetLogger().LogError(ex, "Edit lab execution");
            AddStatusMessage(StatusMessageType.Error, Localizer["EditLabExecutionAsync:UnknownError"]);
            return await ShowEditLabExecutionFormAsync(labExecutionInput);
        }
    }

    private async Task<IActionResult> ShowCreateLabExecutionFormAsync(AdminLabExecutionInputModel labExecutionInput)
    {
        ViewData["Labs"] = await labService.GetLabsAsync(HttpContext.RequestAborted);
        ViewData["Groups"] = await groupService.GetGroupsAsync(HttpContext.RequestAborted);

        return await RenderViewAsync("~/Views/Admin/LabExecutions/Create.cshtml", labExecutionInput);
    }
    
    [HttpGet("create")]
    [AnyUserPrivilege(UserPrivileges.EditLabExecutions)]
    public Task<IActionResult> ShowCreateLabExecutionFormAsync()
        => ShowCreateLabExecutionFormAsync(null);

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabExecutions)]
    public async Task<IActionResult> CreateLabExecutionAsync(AdminLabExecutionInputModel labExecutionInput, [FromServices] IMapper mapper)
    {
        // Check input
        if(!ModelState.IsValid
           || !await labService.LabExistsAsync(labExecutionInput.LabId, HttpContext.RequestAborted)
           || !await groupService.GroupExistsAsync(labExecutionInput.GroupId, HttpContext.RequestAborted)
           || !(labExecutionInput.Start < labExecutionInput.End))
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateLabExecutionAsync:InvalidInput"]);
            return await ShowCreateLabExecutionFormAsync(labExecutionInput);
        }

        // Start lab for group
        try
        {
            var labExecution = mapper.Map<LabExecution>(labExecutionInput);
            await labExecutionService.CreateLabExecutionAsync(labExecution, false, HttpContext.RequestAborted);

            PostStatusMessage = new StatusMessage(StatusMessageType.Success, Localizer["CreateLabExecutionAsync:Success"]) { AutoHide = true };
            return RedirectToAction("RenderLabExecutionList");
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Create lab execution for group");
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateLabExecutionAsync:UnknownError"]);
            return await ShowCreateLabExecutionFormAsync(labExecutionInput);
        }
    }
    
    private async Task<IActionResult> ShowCreateMultipleLabExecutionsFormAsync(AdminLabExecutionMultipleInputModel labExecutionInput)
    {
        ViewData["Labs"] = await labService.GetLabsAsync(HttpContext.RequestAborted);
        ViewData["Slots"] = await slotService.GetSlotsAsync(HttpContext.RequestAborted);

        return await RenderViewAsync("~/Views/Admin/LabExecutions/CreateMultiple.cshtml", labExecutionInput);
    }
    
    [HttpGet("create-multiple")]
    [AnyUserPrivilege(UserPrivileges.EditLabExecutions)]
    public Task<IActionResult> ShowCreateMultipleLabExecutionsFormAsync()
        => ShowCreateMultipleLabExecutionsFormAsync(null);
    
    [HttpPost("create-multiple")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabExecutions)]
    public async Task<IActionResult> CreateMultipleLabExecutionsAsync(AdminLabExecutionMultipleInputModel labExecutionInput, [FromServices] IMapper mapper)
    {
        // Check input
        if(!ModelState.IsValid
           || !await labService.LabExistsAsync(labExecutionInput.LabId, HttpContext.RequestAborted)
           || !await slotService.SlotExistsAsync(labExecutionInput.SlotId, HttpContext.RequestAborted)
           || !(labExecutionInput.Start < labExecutionInput.End))
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateMultipleLabExecutionsAsync:InvalidInput"]);
            return await ShowCreateMultipleLabExecutionsFormAsync(labExecutionInput);
        }

        bool anyIssue = false;
        try
        {
            // Start lab for each of the groups
            foreach(var group in await groupService.GetGroupsInSlotAsync(labExecutionInput.SlotId, HttpContext.RequestAborted))
            {
                try
                {
                    var labExecution = mapper.Map<LabExecution>(labExecutionInput);
                    labExecution.GroupId = group.Id;
                    await labExecutionService.CreateLabExecutionAsync(labExecution, labExecutionInput.OverrideExisting, HttpContext.RequestAborted);
                }
                catch(Exception ex)
                {
                    GetLogger().LogError(ex, "Create lab execution for group in slot");
                    AddStatusMessage(StatusMessageType.Warning, Localizer["CreateMultipleLabExecutionsAsync:ErrorGroup", group.Id, group.DisplayName]);
                    anyIssue = true;
                }
            }

            if(anyIssue)
            {
                AddStatusMessage(StatusMessageType.Success, Localizer["CreateMultipleLabExecutionsAsync:Success"]);
                return await RenderLabExecutionListAsync();
            }
            else
            {
                PostStatusMessage = new StatusMessage(StatusMessageType.Success, Localizer["CreateMultipleLabExecutionsAsync:Success"]) { AutoHide = true };
                return RedirectToAction("RenderLabExecutionList");
            }
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Create lab execution for slot");
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateMultipleLabExecutionsAsync:UnknownError"]);
            return await ShowCreateMultipleLabExecutionsFormAsync(labExecutionInput);
        }
    }

    [HttpPost("delete")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabExecutions)]
    public async Task<IActionResult> DeleteLabExecutionAsync(int groupId, int labId)
    {
        try
        {
            // Input check
            var labExecution = await labExecutionService.FindLabExecutionAsync(groupId, labId, HttpContext.RequestAborted);
            if(labExecution == null)
            {
                AddStatusMessage(StatusMessageType.Error, Localizer["DeleteLabExecutionAsync:NotFound"]);
                return RedirectToAction("RenderLabExecutionList");
            }
            
            // Delete execution
            await labExecutionService.DeleteLabExecutionAsync(groupId, labId, HttpContext.RequestAborted);

            PostStatusMessage = new StatusMessage(StatusMessageType.Success, Localizer["DeleteLabExecutionAsync:Success"]) { AutoHide = true };
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Delete lab execution for group");
            PostStatusMessage = new StatusMessage(StatusMessageType.Error, Localizer["DeleteLabExecutionAsync:UnknownError"]);
        }

        return RedirectToAction("RenderLabExecutionList");
    }

    [HttpPost("delete-multiple")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabExecutions)]
    public async Task<IActionResult> DeleteMultipleLabExecutionsAsync(int slotId, int labId)
    {
        try
        {
            // Delete all executions for the given slot
            await labExecutionService.DeleteLabExecutionsForSlotAsync(slotId, labId, HttpContext.RequestAborted);

            PostStatusMessage = new StatusMessage(StatusMessageType.Success, Localizer["DeleteMultipleLabExecutionsAsync:Success"]) { AutoHide = true };
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Delete lab execution for slot");
            PostStatusMessage = new StatusMessage(StatusMessageType.Error, Localizer["DeleteMultipleLabExecutionsAsync:UnknownError"]);
        }

        return RedirectToAction("RenderLabExecutionList");
    }

    public static void RegisterMappings(Profile profile)
    {
        profile.CreateMap<LabExecution, AdminLabExecutionInputModel>();
        profile.CreateMap<AdminLabExecutionInputModel, LabExecution>();
        
        profile.CreateMap<AdminLabExecutionMultipleInputModel, LabExecution>();
    }
}
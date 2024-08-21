using System;
using System.Linq;
using System.Threading.Tasks;
using Ctf4e.Server.Authorization;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Models;
using Ctf4e.Server.Services;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Ctf4e.Server.Controllers;

[Route("admin/slots")]
[AnyUserPrivilege(UserPrivileges.ViewSlots)]
public class AdminSlotsController(IUserService userService, ISlotService slotService)
    : ControllerBase<AdminSlotsController>(userService)
{
    protected override MenuItems ActiveMenuItem => MenuItems.AdminSlots;

    private Task<IActionResult> RenderAsync(ViewType viewType, string viewPath, object model)
    {
        ViewData["ViewType"] = viewType;
        return RenderViewAsync(viewPath, model);
    }

    [HttpGet]
    public async Task<IActionResult> RenderSlotListAsync()
    {
        // Pass slots
        var slots = await slotService.GetSlotsAsync(HttpContext.RequestAborted);

        return await RenderAsync(ViewType.List, "~/Views/AdminSlots.cshtml", slots);
    }

    private async Task<IActionResult> ShowEditSlotFormAsync(int? id, Slot slot = null)
    {
        // Retrieve by ID, if no object from a failed POST was passed
        if(id != null)
        {
            slot = await slotService.FindSlotByIdAsync(id.Value, HttpContext.RequestAborted);
            if(slot == null)
            {
                AddStatusMessage(StatusMessageType.Error, Localizer["ShowEditSlotFormAsync:NotFound"]);
                return await RenderSlotListAsync();
            }
        }

        if(slot == null)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["ShowEditSlotFormAsync:MissingParameter"]);
            return await RenderSlotListAsync();
        }

        return await RenderAsync(ViewType.Edit, "~/Views/AdminSlots.cshtml", slot);
    }

    [HttpGet("edit")]
    [AnyUserPrivilege(UserPrivileges.EditSlots)]
    public Task<IActionResult> ShowEditSlotFormAsync(int id)
    {
        return ShowEditSlotFormAsync(id, null);
    }

    [HttpPost("edit")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditSlots)]
    public async Task<IActionResult> EditSlotAsync(Slot slotData)
    {
        // Check input
        if(!ModelState.IsValid)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["EditSlotAsync:InvalidInput"]);
            return await ShowEditSlotFormAsync(null, slotData);
        }

        try
        {
            // Retrieve edited slot from database and apply changes
            var slot = await slotService.FindSlotByIdAsync(slotData.Id, HttpContext.RequestAborted);
            slot.Name = slotData.Name;
            slot.DefaultExecuteLabId = slotData.DefaultExecuteLabId;
            slot.DefaultExecuteLabEnd = slotData.DefaultExecuteLabEnd;
            await slotService.UpdateSlotAsync(slot, HttpContext.RequestAborted);

            AddStatusMessage(StatusMessageType.Success, Localizer["EditSlotAsync:Success"]);
        }
        catch(InvalidOperationException ex)
        {
            GetLogger().LogError(ex, "Edit slot");
            AddStatusMessage(StatusMessageType.Error, Localizer["EditSlotAsync:UnknownError"]);
            return await ShowEditSlotFormAsync(null, slotData);
        }

        return await RenderSlotListAsync();
    }

    [HttpGet("create")]
    [AnyUserPrivilege(UserPrivileges.EditSlots)]
    public async Task<IActionResult> ShowCreateSlotFormAsync(Slot slot = null)
    {
        return await RenderAsync(ViewType.Create, "~/Views/AdminSlots.cshtml", slot);
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditSlots)]
    public async Task<IActionResult> CreateSlotAsync(Slot slotData)
    {
        // Check input
        if(!ModelState.IsValid)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateSlotAsync:InvalidInput"]);
            return await ShowCreateSlotFormAsync(slotData);
        }

        try
        {
            // Create slot
            var slot = new Slot
            {
                Name = slotData.Name,
                DefaultExecuteLabId = slotData.DefaultExecuteLabId,
                DefaultExecuteLabEnd = slotData.DefaultExecuteLabEnd
            };
            await slotService.CreateSlotAsync(slot, HttpContext.RequestAborted);

            AddStatusMessage(StatusMessageType.Success, Localizer["CreateSlotAsync:Success"]);
        }
        catch(InvalidOperationException ex)
        {
            GetLogger().LogError(ex, "Create slot");
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateSlotAsync:UnknownError"]);
            return await ShowCreateSlotFormAsync(slotData);
        }

        return await RenderSlotListAsync();
    }

    [HttpPost("delete")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditSlots)]
    public async Task<IActionResult> DeleteSlotAsync(int id)
    {
        // Input check
        var slot = await slotService.FindSlotByIdAsync(id, HttpContext.RequestAborted);
        if(slot == null)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["DeleteSlotAsync:NotFound"]);
            return await RenderSlotListAsync();
        }

        if(slot.Groups.Any())
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["DeleteSlotAsync:HasGroups"]);
            return await RenderSlotListAsync();
        }

        try
        {
            // Delete slot
            await slotService.DeleteSlotAsync(id, HttpContext.RequestAborted);

            AddStatusMessage(StatusMessageType.Success, Localizer["DeleteSlotAsync:Success"]);
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Delete slot");
            AddStatusMessage(StatusMessageType.Error, Localizer["DeleteSlotAsync:UnknownError"]);
        }

        return await RenderSlotListAsync();
    }

    public enum ViewType
    {
        List,
        Edit,
        Create
    }
}
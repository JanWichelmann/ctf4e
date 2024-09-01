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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ctf4e.Server.Controllers;

[Route("admin/slots")]
[AnyUserPrivilege(UserPrivileges.ViewSlots)]
public class AdminSlotsController(IUserService userService, ISlotService slotService)
    : ControllerBase<AdminSlotsController>(userService)
{
    protected override MenuItems ActiveMenuItem => MenuItems.AdminSlots;

    [HttpGet]
    public async Task<IActionResult> RenderSlotListAsync()
    {
        // Pass slots
        var slots = await slotService.GetSlotListAsync(HttpContext.RequestAborted);

        return await RenderViewAsync("~/Views/Admin/Slots/Index.cshtml", slots);
    }

    private async Task<IActionResult> ShowEditSlotFormAsync(AdminSlotInputModel slotInput)
    {
        var labService = HttpContext.RequestServices.GetRequiredService<ILabService>();
        ViewData["Labs"] = await labService.GetLabsAsync(HttpContext.RequestAborted);

        return await RenderViewAsync("~/Views/Admin/Slots/Edit.cshtml", slotInput);
    }

    [HttpGet("edit")]
    [AnyUserPrivilege(UserPrivileges.EditSlots)]
    public async Task<IActionResult> ShowEditSlotFormAsync(int id, [FromServices] IMapper mapper)
    {
        var slot = await slotService.FindSlotByIdAsync(id, HttpContext.RequestAborted);
        if(slot == null)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["ShowEditSlotFormAsync:NotFound"]);
            return await RenderSlotListAsync();
        }

        var slotInput = mapper.Map<AdminSlotInputModel>(slot);
        return await ShowEditSlotFormAsync(slotInput);
    }

    [HttpPost("edit")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditSlots)]
    public async Task<IActionResult> EditSlotAsync(AdminSlotInputModel slotInput, [FromServices] IMapper mapper)
    {
        // Check input
        if(!ModelState.IsValid || slotInput.Id == null)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["EditSlotAsync:InvalidInput"]);
            return await ShowEditSlotFormAsync(slotInput);
        }

        try
        {
            // Retrieve edited slot from database and apply changes
            var slot = await slotService.FindSlotByIdAsync(slotInput.Id.Value, HttpContext.RequestAborted);
            if(slot == null)
            {
                PostStatusMessage = new StatusMessage(StatusMessageType.Error, Localizer["EditSlotAsync:NotFound"]);
                return RedirectToAction("RenderSlotList");
            }

            mapper.Map(slotInput, slot);

            await slotService.UpdateSlotAsync(slot, HttpContext.RequestAborted);

            PostStatusMessage = new StatusMessage(StatusMessageType.Success, Localizer["EditSlotAsync:Success"]) { AutoHide = true };
            return RedirectToAction("RenderSlotList");
        }
        catch(InvalidOperationException ex)
        {
            GetLogger().LogError(ex, "Edit slot");
            AddStatusMessage(StatusMessageType.Error, Localizer["EditSlotAsync:UnknownError"]);
            return await ShowEditSlotFormAsync(slotInput);
        }
    }

    private async Task<IActionResult> ShowCreateSlotFormAsync(AdminSlotInputModel slotInput)
    {
        var labService = HttpContext.RequestServices.GetRequiredService<ILabService>();
        ViewData["Labs"] = await labService.GetLabsAsync(HttpContext.RequestAborted);

        return await RenderViewAsync("~/Views/Admin/Slots/Create.cshtml", slotInput);
    }

    [HttpGet("create")]
    [AnyUserPrivilege(UserPrivileges.EditSlots)]
    public Task<IActionResult> ShowCreateSlotFormAsync()
        => ShowCreateSlotFormAsync(null);

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditSlots)]
    public async Task<IActionResult> CreateSlotAsync(AdminSlotInputModel slotInput, [FromServices] IMapper mapper)
    {
        // Check input
        if(!ModelState.IsValid)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateSlotAsync:InvalidInput"]);
            return await ShowCreateSlotFormAsync(slotInput);
        }

        try
        {
            // Create slot
            var slot = mapper.Map<Slot>(slotInput);
            await slotService.CreateSlotAsync(slot, HttpContext.RequestAborted);

            PostStatusMessage = new StatusMessage(StatusMessageType.Success, Localizer["CreateSlotAsync:Success"]) { AutoHide = true };
            return RedirectToAction("RenderSlotList");
        }
        catch(InvalidOperationException ex)
        {
            GetLogger().LogError(ex, "Create slot");
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateSlotAsync:UnknownError"]);
            return await ShowCreateSlotFormAsync(slotInput);
        }
    }

    [HttpPost("delete")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditSlots)]
    public async Task<IActionResult> DeleteSlotAsync(int id)
    {
        try
        {
            // Input check
            var slot = await slotService.FindSlotByIdAsync(id, HttpContext.RequestAborted);
            if(slot == null)
            {
                PostStatusMessage = new StatusMessage(StatusMessageType.Error, Localizer["DeleteSlotAsync:NotFound"]);
                return RedirectToAction("RenderSlotList");
            }

            if(await slotService.GetSlotGroupCount(id, HttpContext.RequestAborted) != 0)
            {
                PostStatusMessage = new StatusMessage(StatusMessageType.Error, Localizer["DeleteSlotAsync:HasGroups"]);
                return RedirectToAction("RenderSlotList");
            }

            // Delete slot
            await slotService.DeleteSlotAsync(id, HttpContext.RequestAborted);

            PostStatusMessage = new StatusMessage(StatusMessageType.Success, Localizer["DeleteSlotAsync:Success"]) { AutoHide = true };
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Delete slot");
            PostStatusMessage = new StatusMessage(StatusMessageType.Error, Localizer["DeleteSlotAsync:UnknownError"]);
        }

        return RedirectToAction("RenderSlotList");
    }

    public static void RegisterMappings(Profile mappingProfile)
    {
        mappingProfile.CreateMap<Slot, AdminSlotInputModel>();
        mappingProfile.CreateMap<AdminSlotInputModel, Slot>();
    }
}
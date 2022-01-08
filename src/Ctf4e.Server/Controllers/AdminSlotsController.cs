using System;
using System.Linq;
using System.Threading.Tasks;
using Ctf4e.Server.Authorization;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Models;
using Ctf4e.Server.Services;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Ctf4e.Server.Controllers;

[Route("admin/slots")]
[AnyUserPrivilege(UserPrivileges.ViewSlots)]
public class AdminSlotsController : ControllerBase
{
    private readonly IStringLocalizer<AdminSlotsController> _localizer;
    private readonly ILogger<AdminSlotsController> _logger;
    private readonly ISlotService _slotService;

    public AdminSlotsController(IUserService userService, IStringLocalizer<AdminSlotsController> localizer, ILogger<AdminSlotsController> logger, ISlotService slotService)
        : base("~/Views/AdminSlots.cshtml", userService)
    {
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _slotService = slotService ?? throw new ArgumentNullException(nameof(slotService));
    }

    private Task<IActionResult> RenderAsync(ViewType viewType, object model)
    {
        ViewData["ViewType"] = viewType;
        return RenderViewAsync(MenuItems.AdminSlots, model);
    }

    [HttpGet]
    public async Task<IActionResult> RenderSlotListAsync()
    {
        // Pass slots
        var slots = await _slotService.GetSlotsAsync().ToListAsync();

        return await RenderAsync(ViewType.List, slots);
    }

    private async Task<IActionResult> ShowEditSlotFormAsync(int? id, Slot slot = null)
    {
        // Retrieve by ID, if no object from a failed POST was passed
        if(id != null)
        {
            slot = await _slotService.GetSlotAsync(id.Value, HttpContext.RequestAborted);
            if(slot == null)
            {
                AddStatusMessage(_localizer["ShowEditSlotFormAsync:NotFound"], StatusMessageTypes.Error);
                return await RenderSlotListAsync();
            }
        }

        if(slot == null)
        {
            AddStatusMessage(_localizer["ShowEditSlotFormAsync:MissingParameter"], StatusMessageTypes.Error);
            return await RenderSlotListAsync();
        }

        return await RenderAsync(ViewType.Edit, slot);
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
            AddStatusMessage(_localizer["EditSlotAsync:InvalidInput"], StatusMessageTypes.Error);
            return await ShowEditSlotFormAsync(null, slotData);
        }

        try
        {
            // Retrieve edited slot from database and apply changes
            var slot = await _slotService.GetSlotAsync(slotData.Id, HttpContext.RequestAborted);
            slot.Name = slotData.Name;
            await _slotService.UpdateSlotAsync(slot, HttpContext.RequestAborted);

            AddStatusMessage(_localizer["EditSlotAsync:Success"], StatusMessageTypes.Success);
        }
        catch(InvalidOperationException ex)
        {
            _logger.LogError(ex, "Edit slot");
            AddStatusMessage(_localizer["EditSlotAsync:UnknownError"], StatusMessageTypes.Error);
            return await ShowEditSlotFormAsync(null, slotData);
        }

        return await RenderSlotListAsync();
    }

    [HttpGet("create")]
    [AnyUserPrivilege(UserPrivileges.EditSlots)]
    public async Task<IActionResult> ShowCreateSlotFormAsync(Slot slot = null)
    {
        return await RenderAsync(ViewType.Create, slot);
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditSlots)]
    public async Task<IActionResult> CreateSlotAsync(Slot slotData)
    {
        // Check input
        if(!ModelState.IsValid)
        {
            AddStatusMessage(_localizer["CreateSlotAsync:InvalidInput"], StatusMessageTypes.Error);
            return await ShowCreateSlotFormAsync(slotData);
        }

        try
        {
            // Create slot
            var slot = new Slot
            {
                Name = slotData.Name
            };
            await _slotService.CreateSlotAsync(slot, HttpContext.RequestAborted);

            AddStatusMessage(_localizer["CreateSlotAsync:Success"], StatusMessageTypes.Success);
        }
        catch(InvalidOperationException ex)
        {
            _logger.LogError(ex, "Create slot");
            AddStatusMessage(_localizer["CreateSlotAsync:UnknownError"], StatusMessageTypes.Error);
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
        var slot = await _slotService.GetSlotAsync(id, HttpContext.RequestAborted);
        if(slot == null)
        {
            AddStatusMessage(_localizer["DeleteSlotAsync:NotFound"], StatusMessageTypes.Error);
            return await RenderSlotListAsync();
        }

        if(slot.Groups.Any())
        {
            AddStatusMessage(_localizer["DeleteSlotAsync:HasGroups"], StatusMessageTypes.Error);
            return await RenderSlotListAsync();
        }

        try
        {
            // Delete slot
            await _slotService.DeleteSlotAsync(id, HttpContext.RequestAborted);

            AddStatusMessage(_localizer["DeleteSlotAsync:Success"], StatusMessageTypes.Success);
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Delete slot");
            AddStatusMessage(_localizer["DeleteSlotAsync:UnknownError"], StatusMessageTypes.Error);
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
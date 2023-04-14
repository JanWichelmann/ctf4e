using System;
using System.Linq;
using System.Threading.Tasks;
using Ctf4e.Server.Authorization;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Models;
using Ctf4e.Server.Services;
using Ctf4e.Server.ViewModels;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Ctf4e.Server.Controllers;

[Route("admin/executions")]
[AnyUserPrivilege(UserPrivileges.ViewLabExecutions)]
public class AdminLabExecutionsController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IStringLocalizer<AdminLabExecutionsController> _localizer;
    private readonly ILogger<AdminLabExecutionsController> _logger;
    private readonly ILabExecutionService _labExecutionService;
    private readonly ISlotService _slotService;
    private readonly ILabService _labService;

    public AdminLabExecutionsController(IUserService userService, IStringLocalizer<AdminLabExecutionsController> localizer, ILogger<AdminLabExecutionsController> logger, ILabExecutionService labExecutionService, ISlotService slotService, ILabService labService)
        : base("~/Views/AdminLabExecutions.cshtml", userService)
    {
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _labExecutionService = labExecutionService ?? throw new ArgumentNullException(nameof(labExecutionService));
        _slotService = slotService ?? throw new ArgumentNullException(nameof(slotService));
        _labService = labService ?? throw new ArgumentNullException(nameof(labService));
    }

    private Task<IActionResult> RenderAsync(ViewType viewType, object model)
    {
        ViewData["ViewType"] = viewType;
        return RenderViewAsync(MenuItems.AdminLabExecutions, model);
    }

    [HttpGet]
    public async Task<IActionResult> RenderLabExecutionListAsync()
    {
        // Pass data
        var labExecutions = await _labExecutionService.GetLabExecutionsAsync().ToListAsync();
        ViewData["Labs"] = await _labService.GetLabsAsync().ToListAsync();
        ViewData["Slots"] = await _slotService.GetSlotsAsync().ToListAsync();

        return await RenderAsync(ViewType.List, labExecutions);
    }

    private async Task<IActionResult> ShowEditLabExecutionFormAsync(int? groupId, int? labId, AdminLabExecution labExecutionData = null)
    {
        // Retrieve by ID, if no object from a failed POST was passed
        if(groupId != null && labId != null)
        {
            labExecutionData = new AdminLabExecution
            {
                LabExecution = await _labExecutionService.GetLabExecutionAsync(groupId.Value, labId.Value, HttpContext.RequestAborted)
            };
            if(labExecutionData.LabExecution == null)
            {
                AddStatusMessage(_localizer["ShowEditLabExecutionFormAsync:NotFound"], StatusMessageTypes.Error);
                return await RenderLabExecutionListAsync();
            }
        }

        if(labExecutionData?.LabExecution == null)
        {
            AddStatusMessage(_localizer["ShowEditLabExecutionFormAsync:MissingParameter"], StatusMessageTypes.Error);
            return await RenderLabExecutionListAsync();
        }

        return await RenderAsync(ViewType.Edit, labExecutionData);
    }

    [HttpGet("edit")]
    [AnyUserPrivilege(UserPrivileges.EditLabExecutions)]
    public Task<IActionResult> ShowEditLabExecutionFormAsync(int groupId, int labId)
    {
        // Always show warning
        AddStatusMessage(_localizer["ShowEditLabExecutionFormAsync:Warning"], StatusMessageTypes.Warning);

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
            AddStatusMessage(_localizer["EditLabExecutionAsync:InvalidInput"], StatusMessageTypes.Error);
            return await ShowEditLabExecutionFormAsync(null, null, labExecutionData);
        }

        try
        {
            // Retrieve edited labExecution from database and apply changes
            var labExecution = await _labExecutionService.GetLabExecutionAsync(labExecutionData.LabExecution.GroupId, labExecutionData.LabExecution.LabId, HttpContext.RequestAborted);
            labExecution.Start = labExecutionData.LabExecution.Start;
            labExecution.End = labExecutionData.LabExecution.End;
            await _labExecutionService.UpdateLabExecutionAsync(labExecution, HttpContext.RequestAborted);

            AddStatusMessage(_localizer["EditLabExecutionAsync:Success"], StatusMessageTypes.Success);
        }
        catch(InvalidOperationException ex)
        {
            _logger.LogError(ex, "Edit lab execution");
            AddStatusMessage(_localizer["EditLabExecutionAsync:UnknownError"], StatusMessageTypes.Error);
            return await ShowEditLabExecutionFormAsync(null, null, labExecutionData);
        }

        return await RenderLabExecutionListAsync();
    }

    [HttpGet("create/slot")]
    [AnyUserPrivilege(UserPrivileges.EditLabExecutions)]
    public async Task<IActionResult> ShowCreateLabExecutionForSlotFormAsync(AdminLabExecution labExecutionData = null)
    {
        // Pass lists
        ViewData["Labs"] = await _labService.GetLabsAsync().ToListAsync();
        ViewData["Slots"] = await _slotService.GetSlotsAsync().ToListAsync();

        return await RenderAsync(ViewType.CreateForSlot, labExecutionData);
    }

    [HttpPost("create/slot")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabExecutions)]
    public async Task<IActionResult> CreateLabExecutionForSlotAsync(AdminLabExecution labExecutionData)
    {
        // Check input
        if(!ModelState.IsValid
           || !await _labService.LabExistsAsync(labExecutionData.LabExecution.LabId, HttpContext.RequestAborted)
           || !await _slotService.SlotExistsAsync(labExecutionData.SlotId, HttpContext.RequestAborted)
           || !(labExecutionData.LabExecution.Start < labExecutionData.LabExecution.End))
        {
            AddStatusMessage(_localizer["CreateLabExecutionForSlotAsync:InvalidInput"], StatusMessageTypes.Error);
            return await ShowCreateLabExecutionForSlotFormAsync(labExecutionData);
        }

        try
        {
            // Start lab for each of the groups
            foreach(var group in await _userService.GetGroupsInSlotAsync(labExecutionData.SlotId).ToListAsync())
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
                    await _labExecutionService.CreateLabExecutionAsync(labExecution, labExecutionData.OverrideExisting, HttpContext.RequestAborted);
                }
                catch(Exception ex)
                {
                    _logger.LogError(ex, "Create lab execution for group in slot");
                    AddStatusMessage(_localizer["CreateLabExecutionForSlotAsync:ErrorGroup", group.Id, group.DisplayName], StatusMessageTypes.Warning);
                }
            }

            AddStatusMessage(_localizer["CreateLabExecutionForSlotAsync:Success"], StatusMessageTypes.Success);
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Create lab execution for slot");
            AddStatusMessage(_localizer["CreateLabExecutionForSlotAsync:UnknownError"], StatusMessageTypes.Error);
            return await ShowCreateLabExecutionForSlotFormAsync(labExecutionData);
        }

        return await RenderLabExecutionListAsync();
    }

    [HttpGet("create/group")]
    [AnyUserPrivilege(UserPrivileges.EditLabExecutions)]
    public async Task<IActionResult> ShowCreateLabExecutionForGroupFormAsync(AdminLabExecution labExecutionData = null)
    {
        // Pass lists
        ViewData["Labs"] = await _labService.GetLabsAsync().ToListAsync();
        ViewData["Groups"] = await _userService.GetGroupsAsync().ToListAsync();

        return await RenderAsync(ViewType.CreateForGroup, labExecutionData);
    }

    [HttpPost("create/group")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabExecutions)]
    public async Task<IActionResult> CreateLabExecutionForGroupAsync(AdminLabExecution labExecutionData)
    {
        // Check input
        if(!ModelState.IsValid
           || !await _labService.LabExistsAsync(labExecutionData.LabExecution.LabId, HttpContext.RequestAborted)
           || !await _userService.GroupExistsAsync(labExecutionData.LabExecution.GroupId, HttpContext.RequestAborted)
           || !(labExecutionData.LabExecution.Start < labExecutionData.LabExecution.End))
        {
            AddStatusMessage(_localizer["CreateLabExecutionForGroupAsync:InvalidInput"], StatusMessageTypes.Error);
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
            await _labExecutionService.CreateLabExecutionAsync(labExecution, labExecutionData.OverrideExisting, HttpContext.RequestAborted);

            AddStatusMessage(_localizer["CreateLabExecutionForGroupAsync:Success"], StatusMessageTypes.Success);
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Create lab execution for group");
            AddStatusMessage(_localizer["CreateLabExecutionForGroupAsync:UnknownError"], StatusMessageTypes.Error);
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
        var labExecution = await _labExecutionService.GetLabExecutionAsync(groupId, labId, HttpContext.RequestAborted);
        if(labExecution == null)
        {
            AddStatusMessage(_localizer["DeleteLabExecutionForGroupAsync:NotFound"], StatusMessageTypes.Error);
            return await RenderLabExecutionListAsync();
        }

        try
        {
            // Delete execution
            await _labExecutionService.DeleteLabExecutionAsync(groupId, labId, HttpContext.RequestAborted);

            AddStatusMessage(_localizer["DeleteLabExecutionForGroupAsync:Success"], StatusMessageTypes.Success);
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Delete lab execution for group");
            AddStatusMessage(_localizer["DeleteLabExecutionForGroupAsync:UnknownError"], StatusMessageTypes.Error);
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
            await _labExecutionService.DeleteLabExecutionsForSlotAsync(slotId, labId, HttpContext.RequestAborted);

            AddStatusMessage(_localizer["DeleteLabExecutionForSlotAsync:Success"], StatusMessageTypes.Success);
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Delete lab execution for slot");
            AddStatusMessage(_localizer["DeleteLabExecutionForSlotAsync:UnknownError"], StatusMessageTypes.Error);
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
using System;
using System.Linq;
using System.Threading.Tasks;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Models;
using Ctf4e.Server.Options;
using Ctf4e.Server.Services;
using Ctf4e.Server.ViewModels;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ctf4e.Server.Controllers
{
    [Route("admin/executions")]
    [Authorize(Policy = AuthenticationStrings.PolicyIsAdmin)]
    public class AdminLabExecutionsController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILabExecutionService _labExecutionService;
        private readonly ISlotService _slotService;
        private readonly ILabService _labService;

        public AdminLabExecutionsController(IUserService userService, IOptions<MainOptions> mainOptions, ILabExecutionService labExecutionService, ISlotService slotService, ILabService labService)
            : base(userService, mainOptions, "~/Views/AdminLabExecutions.cshtml")
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _labExecutionService = labExecutionService ?? throw new ArgumentNullException(nameof(labExecutionService));
            _slotService = slotService ?? throw new ArgumentNullException(nameof(slotService));
            _labService = labService ?? throw new ArgumentNullException(nameof(labService));
        }

        private Task<IActionResult> RenderAsync(ViewType viewType, object model = null)
        {
            ViewData["ViewType"] = viewType;
            return RenderViewAsync(MenuItems.AdminLabExecutions, model);
        }

        [HttpGet]
        public async Task<IActionResult> RenderLabExecutionListAsync()
        {
            // Pass data
            ViewData["LabExecutions"] = await _labExecutionService.GetLabExecutionsAsync().ToListAsync();
            ViewData["Labs"] = await _labService.GetLabsAsync().ToListAsync();
            ViewData["Slots"] = await _slotService.GetSlotsAsync().ToListAsync();

            return await RenderAsync(ViewType.ListLabExecutions);
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
                    AddStatusMessage("Diese Ausführung existiert nicht.", StatusMessageTypes.Error);
                    return await RenderLabExecutionListAsync();
                }
            }
            if(labExecutionData?.LabExecution == null)
            {
                AddStatusMessage("Keine Ausführung übergeben.", StatusMessageTypes.Error);
                return await RenderLabExecutionListAsync();
            }

            return await RenderAsync(ViewType.EditLabExecution, labExecutionData);
        }

        [HttpGet("edit")]
        public Task<IActionResult> ShowEditLabExecutionFormAsync(int groupId, int labId)
        {
            // Always show warning
            AddStatusMessage("Achtung: Das Bearbeiten einer bereits aktiven Praktikumsausführung kann zu Änderungen des \"Bestanden\"-Status und Verschiebungen im Scoreboard führen. "
                             + "Aufgaben- und Flag-Einreichungen werden nicht gelöscht, auch wenn diese bezüglich der neuen Beginn- und Endzeiten nicht möglich gewesen wären.",
                StatusMessageTypes.Warning);

            return ShowEditLabExecutionFormAsync(groupId, labId, null);
        }

        [HttpPost("edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditLabExecutionAsync(AdminLabExecution labExecutionData)
        {
            // Check input
            if(!ModelState.IsValid || !(labExecutionData.LabExecution.PreStart < labExecutionData.LabExecution.Start &&
                                        labExecutionData.LabExecution.Start < labExecutionData.LabExecution.End))
            {
                AddStatusMessage("Ungültige Eingabe.", StatusMessageTypes.Error);
                return await ShowEditLabExecutionFormAsync(null, null, labExecutionData);
            }

            try
            {
                // Retrieve edited labExecution from database and apply changes
                var labExecution = await _labExecutionService.GetLabExecutionAsync(labExecutionData.LabExecution.GroupId, labExecutionData.LabExecution.LabId, HttpContext.RequestAborted);
                labExecution.PreStart = labExecutionData.LabExecution.PreStart;
                labExecution.Start = labExecutionData.LabExecution.Start;
                labExecution.End = labExecutionData.LabExecution.End;
                await _labExecutionService.UpdateLabExecutionAsync(labExecution, HttpContext.RequestAborted);

                AddStatusMessage("Änderungen gespeichert.", StatusMessageTypes.Success);
            }
            catch(InvalidOperationException ex)
            {
                AddStatusMessage("Fehler: " + ex.Message, StatusMessageTypes.Error);
                return await ShowEditLabExecutionFormAsync(null, null, labExecutionData);
            }

            return await RenderLabExecutionListAsync();
        }

        [HttpGet("create/slot")]
        public async Task<IActionResult> ShowCreateLabExecutionForSlotFormAsync(AdminLabExecution labExecutionData = null)
        {
            // Pass lists
            ViewData["Labs"] = await _labService.GetLabsAsync().ToListAsync();
            ViewData["Slots"] = await _slotService.GetSlotsAsync().ToListAsync();

            return await RenderAsync(ViewType.CreateLabExecutionForSlot, labExecutionData);
        }

        [HttpPost("create/slot")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLabExecutionForSlotAsync(AdminLabExecution labExecutionData)
        {
            // Check input
            if(!ModelState.IsValid
               || !await _labService.LabExistsAsync(labExecutionData.LabExecution.LabId, HttpContext.RequestAborted)
               || !await _slotService.SlotExistsAsync(labExecutionData.SlotId, HttpContext.RequestAborted)
               || !(labExecutionData.LabExecution.PreStart < labExecutionData.LabExecution.Start &&
                    labExecutionData.LabExecution.Start < labExecutionData.LabExecution.End))
            {
                AddStatusMessage("Ungültige Eingabe.", StatusMessageTypes.Error);
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
                            PreStart = labExecutionData.LabExecution.PreStart,
                            Start = labExecutionData.LabExecution.Start,
                            End = labExecutionData.LabExecution.End
                        };
                        await _labExecutionService.CreateLabExecutionAsync(labExecution, labExecutionData.OverrideExisting, HttpContext.RequestAborted);
                    }
                    catch
                    {
                        AddStatusMessage($"Konnte Praktikum für Gruppe #{group.Id} ({group.DisplayName}) nicht starten. Ist es für diese Gruppe bereits gestartet?", StatusMessageTypes.Warning);
                    }
                }

                AddStatusMessage("Das Praktikum wurde erfolgreich für alle Gruppen im angegebenen Slot gestartet.", StatusMessageTypes.Success);
            }
            catch(InvalidOperationException ex)
            {
                AddStatusMessage("Fehler: " + ex.Message, StatusMessageTypes.Error);
                return await ShowCreateLabExecutionForSlotFormAsync(labExecutionData);
            }

            return await RenderLabExecutionListAsync();
        }

        [HttpGet("create/group")]
        public async Task<IActionResult> ShowCreateLabExecutionForGroupFormAsync(AdminLabExecution labExecutionData = null)
        {
            // Pass lists
            ViewData["Labs"] = await _labService.GetLabsAsync().ToListAsync();
            ViewData["Groups"] = await _userService.GetGroupsAsync().ToListAsync();

            return await RenderAsync(ViewType.CreateLabExecutionForGroup, labExecutionData);
        }

        [HttpPost("create/group")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLabExecutionForGroupAsync(AdminLabExecution labExecutionData)
        {
            // Check input
            if(!ModelState.IsValid
               || !await _labService.LabExistsAsync(labExecutionData.LabExecution.LabId, HttpContext.RequestAborted)
               || !await _userService.GroupExistsAsync(labExecutionData.LabExecution.GroupId, HttpContext.RequestAborted)
               || !(labExecutionData.LabExecution.PreStart < labExecutionData.LabExecution.Start &&
                    labExecutionData.LabExecution.Start < labExecutionData.LabExecution.End))
            {
                AddStatusMessage("Ungültige Eingabe.", StatusMessageTypes.Error);
                return await ShowCreateLabExecutionForGroupFormAsync(labExecutionData);
            }

            // Start lab for group
            try
            {
                var labExecution = new LabExecution
                {
                    GroupId = labExecutionData.LabExecution.GroupId,
                    LabId = labExecutionData.LabExecution.LabId,
                    PreStart = labExecutionData.LabExecution.PreStart,
                    Start = labExecutionData.LabExecution.Start,
                    End = labExecutionData.LabExecution.End
                };
                await _labExecutionService.CreateLabExecutionAsync(labExecution, labExecutionData.OverrideExisting, HttpContext.RequestAborted);

                AddStatusMessage("Das Praktikum wurde erfolgreich für die angegebene Gruppe gestartet.", StatusMessageTypes.Success);
            }
            catch
            {
                AddStatusMessage($"Konnte Praktikum für Gruppe #{labExecutionData.LabExecution.GroupId} nicht starten. Ist es für diese Gruppe bereits gestartet?", StatusMessageTypes.Error);
                return await ShowCreateLabExecutionForGroupFormAsync(labExecutionData);
            }

            return await RenderLabExecutionListAsync();
        }

        [HttpPost("delete/group")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteLabExecutionForGroupAsync(int groupId, int labId)
        {
            // Input check
            var labExecution = await _labExecutionService.GetLabExecutionAsync(groupId, labId, HttpContext.RequestAborted);
            if(labExecution == null)
            {
                AddStatusMessage("Diese Ausführung existiert nicht.", StatusMessageTypes.Error);
                return await RenderLabExecutionListAsync();
            }

            try
            {
                // Delete execution
                await _labExecutionService.DeleteLabExecutionAsync(groupId, labId, HttpContext.RequestAborted);

                AddStatusMessage("Die Ausführung wurde erfolgreich gelöscht.", StatusMessageTypes.Success);
            }
            catch(Exception ex)
            {
                AddStatusMessage("Fehler: " + ex, StatusMessageTypes.Error);
            }

            return await RenderLabExecutionListAsync();
        }

        [HttpPost("delete/slot")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteLabExecutionForSlotAsync(int slotId, int labId)
        {
            try
            {
                // Delete all executions for the given slot
                await _labExecutionService.DeleteLabExecutionsForSlotAsync(slotId, labId, HttpContext.RequestAborted);

                AddStatusMessage("Die Ausführungen wurden erfolgreich gelöscht.", StatusMessageTypes.Success);
            }
            catch(Exception ex)
            {
                AddStatusMessage("Fehler: " + ex, StatusMessageTypes.Error);
            }

            return await RenderLabExecutionListAsync();
        }

        public enum ViewType
        {
            ListLabExecutions,
            EditLabExecution,
            CreateLabExecutionForSlot,
            CreateLabExecutionForGroup
        }
    }
}

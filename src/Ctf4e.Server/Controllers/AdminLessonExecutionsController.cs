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
    public class AdminLessonExecutionsController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILessonExecutionService _lessonExecutionService;
        private readonly ISlotService _slotService;
        private readonly ILessonService _lessonService;

        public AdminLessonExecutionsController(IUserService userService, IOptions<MainOptions> mainOptions, ILessonExecutionService lessonExecutionService, ISlotService slotService, ILessonService lessonService)
            : base("~/Views/AdminLessonExecutions.cshtml", userService, mainOptions)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _lessonExecutionService = lessonExecutionService ?? throw new ArgumentNullException(nameof(lessonExecutionService));
            _slotService = slotService ?? throw new ArgumentNullException(nameof(slotService));
            _lessonService = lessonService ?? throw new ArgumentNullException(nameof(lessonService));
        }

        private Task<IActionResult> RenderAsync(ViewType viewType, object model)
        {
            ViewData["ViewType"] = viewType;
            return RenderViewAsync(MenuItems.AdminLessonExecutions, model);
        }

        [HttpGet]
        public async Task<IActionResult> RenderLessonExecutionListAsync()
        {
            // Pass data
            var lessonExecutions = await _lessonExecutionService.GetLessonExecutionsAsync().ToListAsync();
            ViewData["Lessons"] = await _lessonService.GetLessonsAsync().ToListAsync();
            ViewData["Slots"] = await _slotService.GetSlotsAsync().ToListAsync();

            return await RenderAsync(ViewType.List, lessonExecutions);
        }

        private async Task<IActionResult> ShowEditLessonExecutionFormAsync(int? groupId, int? lessonId, AdminLessonExecution lessonExecutionData = null)
        {
            // Retrieve by ID, if no object from a failed POST was passed
            if(groupId != null && lessonId != null)
            {
                lessonExecutionData = new AdminLessonExecution
                {
                    LessonExecution = await _lessonExecutionService.GetLessonExecutionAsync(groupId.Value, lessonId.Value, HttpContext.RequestAborted)
                };
                if(lessonExecutionData.LessonExecution == null)
                {
                    AddStatusMessage("Diese Ausführung existiert nicht.", StatusMessageTypes.Error);
                    return await RenderLessonExecutionListAsync();
                }
            }

            if(lessonExecutionData?.LessonExecution == null)
            {
                AddStatusMessage("Keine Ausführung übergeben.", StatusMessageTypes.Error);
                return await RenderLessonExecutionListAsync();
            }

            return await RenderAsync(ViewType.Edit, lessonExecutionData);
        }

        [HttpGet("edit")]
        public Task<IActionResult> ShowEditLessonExecutionFormAsync(int groupId, int lessonId)
        {
            // Always show warning
            AddStatusMessage("Achtung: Das Bearbeiten einer bereits aktiven Praktikumsausführung kann zu Änderungen des \"Bestanden\"-Status und Verschiebungen im Scoreboard führen. "
                             + "Aufgaben- und Flag-Einreichungen werden nicht gelöscht, auch wenn diese bezüglich der neuen Beginn- und Endzeiten nicht möglich gewesen wären.",
                             StatusMessageTypes.Warning);

            return ShowEditLessonExecutionFormAsync(groupId, lessonId, null);
        }

        [HttpPost("edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditLessonExecutionAsync(AdminLessonExecution lessonExecutionData)
        {
            // Check input
            if(!ModelState.IsValid || !(lessonExecutionData.LessonExecution.PreStart < lessonExecutionData.LessonExecution.Start &&
                                        lessonExecutionData.LessonExecution.Start < lessonExecutionData.LessonExecution.End))
            {
                AddStatusMessage("Ungültige Eingabe.", StatusMessageTypes.Error);
                return await ShowEditLessonExecutionFormAsync(null, null, lessonExecutionData);
            }

            try
            {
                // Retrieve edited lessonExecution from database and apply changes
                var lessonExecution = await _lessonExecutionService.GetLessonExecutionAsync(lessonExecutionData.LessonExecution.GroupId, lessonExecutionData.LessonExecution.LessonId, HttpContext.RequestAborted);
                lessonExecution.PreStart = lessonExecutionData.LessonExecution.PreStart;
                lessonExecution.Start = lessonExecutionData.LessonExecution.Start;
                lessonExecution.End = lessonExecutionData.LessonExecution.End;
                await _lessonExecutionService.UpdateLessonExecutionAsync(lessonExecution, HttpContext.RequestAborted);

                AddStatusMessage("Änderungen gespeichert.", StatusMessageTypes.Success);
            }
            catch(InvalidOperationException ex)
            {
                AddStatusMessage(ex.Message, StatusMessageTypes.Error);
                return await ShowEditLessonExecutionFormAsync(null, null, lessonExecutionData);
            }

            return await RenderLessonExecutionListAsync();
        }

        [HttpGet("create/slot")]
        public async Task<IActionResult> ShowCreateLessonExecutionForSlotFormAsync(AdminLessonExecution lessonExecutionData = null)
        {
            // Pass lists
            ViewData["Lessons"] = await _lessonService.GetLessonsAsync().ToListAsync();
            ViewData["Slots"] = await _slotService.GetSlotsAsync().ToListAsync();

            return await RenderAsync(ViewType.CreateForSlot, lessonExecutionData);
        }

        [HttpPost("create/slot")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLessonExecutionForSlotAsync(AdminLessonExecution lessonExecutionData)
        {
            // Check input
            if(!ModelState.IsValid
               || !await _lessonService.LessonExistsAsync(lessonExecutionData.LessonExecution.LessonId, HttpContext.RequestAborted)
               || !await _slotService.SlotExistsAsync(lessonExecutionData.SlotId, HttpContext.RequestAborted)
               || !(lessonExecutionData.LessonExecution.PreStart < lessonExecutionData.LessonExecution.Start &&
                    lessonExecutionData.LessonExecution.Start < lessonExecutionData.LessonExecution.End))
            {
                AddStatusMessage("Ungültige Eingabe.", StatusMessageTypes.Error);
                return await ShowCreateLessonExecutionForSlotFormAsync(lessonExecutionData);
            }

            try
            {
                // Start lesson for each of the groups
                foreach(var group in await _userService.GetGroupsInSlotAsync(lessonExecutionData.SlotId).ToListAsync())
                {
                    try
                    {
                        var lessonExecution = new LessonExecution
                        {
                            GroupId = group.Id,
                            LessonId = lessonExecutionData.LessonExecution.LessonId,
                            PreStart = lessonExecutionData.LessonExecution.PreStart,
                            Start = lessonExecutionData.LessonExecution.Start,
                            End = lessonExecutionData.LessonExecution.End
                        };
                        await _lessonExecutionService.CreateLessonExecutionAsync(lessonExecution, lessonExecutionData.OverrideExisting, HttpContext.RequestAborted);
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
                AddStatusMessage(ex.Message, StatusMessageTypes.Error);
                return await ShowCreateLessonExecutionForSlotFormAsync(lessonExecutionData);
            }

            return await RenderLessonExecutionListAsync();
        }

        [HttpGet("create/group")]
        public async Task<IActionResult> ShowCreateLessonExecutionForGroupFormAsync(AdminLessonExecution lessonExecutionData = null)
        {
            // Pass lists
            ViewData["Lessons"] = await _lessonService.GetLessonsAsync().ToListAsync();
            ViewData["Groups"] = await _userService.GetGroupsAsync().ToListAsync();

            return await RenderAsync(ViewType.CreateForGroup, lessonExecutionData);
        }

        [HttpPost("create/group")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLessonExecutionForGroupAsync(AdminLessonExecution lessonExecutionData)
        {
            // Check input
            if(!ModelState.IsValid
               || !await _lessonService.LessonExistsAsync(lessonExecutionData.LessonExecution.LessonId, HttpContext.RequestAborted)
               || !await _userService.GroupExistsAsync(lessonExecutionData.LessonExecution.GroupId, HttpContext.RequestAborted)
               || !(lessonExecutionData.LessonExecution.PreStart < lessonExecutionData.LessonExecution.Start &&
                    lessonExecutionData.LessonExecution.Start < lessonExecutionData.LessonExecution.End))
            {
                AddStatusMessage("Ungültige Eingabe.", StatusMessageTypes.Error);
                return await ShowCreateLessonExecutionForGroupFormAsync(lessonExecutionData);
            }

            // Start lesson for group
            try
            {
                var lessonExecution = new LessonExecution
                {
                    GroupId = lessonExecutionData.LessonExecution.GroupId,
                    LessonId = lessonExecutionData.LessonExecution.LessonId,
                    PreStart = lessonExecutionData.LessonExecution.PreStart,
                    Start = lessonExecutionData.LessonExecution.Start,
                    End = lessonExecutionData.LessonExecution.End
                };
                await _lessonExecutionService.CreateLessonExecutionAsync(lessonExecution, lessonExecutionData.OverrideExisting, HttpContext.RequestAborted);

                AddStatusMessage("Das Praktikum wurde erfolgreich für die angegebene Gruppe gestartet.", StatusMessageTypes.Success);
            }
            catch
            {
                AddStatusMessage($"Konnte Praktikum für Gruppe #{lessonExecutionData.LessonExecution.GroupId} nicht starten. Ist es für diese Gruppe bereits gestartet?", StatusMessageTypes.Error);
                return await ShowCreateLessonExecutionForGroupFormAsync(lessonExecutionData);
            }

            return await RenderLessonExecutionListAsync();
        }

        [HttpPost("delete/group")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteLessonExecutionForGroupAsync(int groupId, int lessonId)
        {
            // Input check
            var lessonExecution = await _lessonExecutionService.GetLessonExecutionAsync(groupId, lessonId, HttpContext.RequestAborted);
            if(lessonExecution == null)
            {
                AddStatusMessage("Diese Ausführung existiert nicht.", StatusMessageTypes.Error);
                return await RenderLessonExecutionListAsync();
            }

            try
            {
                // Delete execution
                await _lessonExecutionService.DeleteLessonExecutionAsync(groupId, lessonId, HttpContext.RequestAborted);

                AddStatusMessage("Die Ausführung wurde erfolgreich gelöscht.", StatusMessageTypes.Success);
            }
            catch(Exception ex)
            {
                AddStatusMessage(ex.ToString(), StatusMessageTypes.Error);
            }

            return await RenderLessonExecutionListAsync();
        }

        [HttpPost("delete/slot")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteLessonExecutionForSlotAsync(int slotId, int lessonId)
        {
            try
            {
                // Delete all executions for the given slot
                await _lessonExecutionService.DeleteLessonExecutionsForSlotAsync(slotId, lessonId, HttpContext.RequestAborted);

                AddStatusMessage("Die Ausführungen wurden erfolgreich gelöscht.", StatusMessageTypes.Success);
            }
            catch(Exception ex)
            {
                AddStatusMessage(ex.ToString(), StatusMessageTypes.Error);
            }

            return await RenderLessonExecutionListAsync();
        }

        public enum ViewType
        {
            List,
            Edit,
            CreateForSlot,
            CreateForGroup
        }
    }
}
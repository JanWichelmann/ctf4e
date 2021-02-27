using System;
using System.Linq;
using System.Threading.Tasks;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Models;
using Ctf4e.Server.Options;
using Ctf4e.Server.Services;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ctf4e.Server.Controllers
{
    [Route("admin/slots")]
    [Authorize(Policy = AuthenticationStrings.PolicyIsAdmin)]
    public class AdminSlotsController : ControllerBase
    {
        private readonly ISlotService _slotService;

        public AdminSlotsController(IUserService userService, IOptions<MainOptions> mainOptions, ISlotService slotService)
            : base("~/Views/AdminSlots.cshtml", userService, mainOptions)
        {
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
                    AddStatusMessage("Dieser Slot existiert nicht.", StatusMessageTypes.Error);
                    return await RenderSlotListAsync();
                }
            }

            if(slot == null)
            {
                AddStatusMessage("Kein Slot übergeben.", StatusMessageTypes.Error);
                return await RenderSlotListAsync();
            }

            return await RenderAsync(ViewType.Edit, slot);
        }

        [HttpGet("edit")]
        public Task<IActionResult> ShowEditSlotFormAsync(int id)
        {
            return ShowEditSlotFormAsync(id, null);
        }

        [HttpPost("edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSlotAsync(Slot slotData)
        {
            // Check input
            if(!ModelState.IsValid)
            {
                AddStatusMessage("Ungültige Eingabe.", StatusMessageTypes.Error);
                return await ShowEditSlotFormAsync(null, slotData);
            }

            try
            {
                // Retrieve edited slot from database and apply changes
                var slot = await _slotService.GetSlotAsync(slotData.Id, HttpContext.RequestAborted);
                slot.Name = slotData.Name;
                await _slotService.UpdateSlotAsync(slot, HttpContext.RequestAborted);

                AddStatusMessage("Änderungen gespeichert.", StatusMessageTypes.Success);
            }
            catch(InvalidOperationException ex)
            {
                AddStatusMessage(ex.Message, StatusMessageTypes.Error);
                return await ShowEditSlotFormAsync(null, slotData);
            }

            return await RenderSlotListAsync();
        }

        [HttpGet("create")]
        public async Task<IActionResult> ShowCreateSlotFormAsync(Slot slot = null)
        {
            return await RenderAsync(ViewType.Create, slot);
        }

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSlotAsync(Slot slotData)
        {
            // Check input
            if(!ModelState.IsValid)
            {
                AddStatusMessage("Ungültige Eingabe.", StatusMessageTypes.Error);
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

                AddStatusMessage("Der Slot wurde erfolgreich erstellt.", StatusMessageTypes.Success);
            }
            catch(InvalidOperationException ex)
            {
                AddStatusMessage(ex.Message, StatusMessageTypes.Error);
                return await ShowCreateSlotFormAsync(slotData);
            }

            return await RenderSlotListAsync();
        }

        [HttpPost("delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSlotAsync(int id)
        {
            // Input check
            var slot = await _slotService.GetSlotAsync(id, HttpContext.RequestAborted);
            if(slot == null)
            {
                AddStatusMessage("Dieser Slot existiert nicht.", StatusMessageTypes.Error);
                return await RenderSlotListAsync();
            }

            if(slot.Groups.Any())
            {
                AddStatusMessage("Der Slot darf keine Gruppen enthalten.", StatusMessageTypes.Error);
                return await RenderSlotListAsync();
            }

            try
            {
                // Delete slot
                await _slotService.DeleteSlotAsync(id, HttpContext.RequestAborted);

                AddStatusMessage("Der Slot wurde erfolgreich gelöscht.", StatusMessageTypes.Success);
            }
            catch(Exception ex)
            {
                AddStatusMessage(ex.ToString(), StatusMessageTypes.Error);
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
}
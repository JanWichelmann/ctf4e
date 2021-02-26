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
    [Route("admin/groups")]
    [Authorize(Policy = AuthenticationStrings.PolicyIsAdmin)]
    public class AdminGroupsController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ISlotService _slotService;

        public AdminGroupsController(IUserService userService, IOptions<MainOptions> mainOptions, ISlotService slotService)
            : base("~/Views/AdminGroups.cshtml", userService, mainOptions)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _slotService = slotService ?? throw new ArgumentNullException(nameof(slotService));
        }

        private Task<IActionResult> RenderAsync(ViewType viewType, object model)
        {
            ViewData["ViewType"] = viewType;
            return RenderViewAsync(MenuItems.AdminGroups, model);
        }

        [HttpGet]
        public async Task<IActionResult> RenderGroupListAsync()
        {
            // Pass groups
            var groups = await _userService.GetGroupsAsync().ToListAsync();

            return await RenderAsync(ViewType.List, groups);
        }

        private async Task<IActionResult> ShowEditGroupFormAsync(int? id, Group group = null)
        {
            // Retrieve by ID, if no object from a failed POST was passed
            if(id != null)
            {
                group = await _userService.GetGroupAsync(id.Value, HttpContext.RequestAborted);
                if(group == null)
                {
                    AddStatusMessage("Diese Gruppe existiert nicht.", StatusMessageTypes.Error);
                    return await RenderGroupListAsync();
                }
            }

            if(group == null)
            {
                AddStatusMessage("Keine Gruppe übergeben.", StatusMessageTypes.Error);
                return await RenderGroupListAsync();
            }

            // Pass list of slots
            ViewData["Slots"] = await _slotService.GetSlotsAsync().ToListAsync();

            return await RenderAsync(ViewType.Edit, group);
        }

        [HttpGet("edit")]
        public Task<IActionResult> ShowEditGroupFormAsync(int id)
        {
            return ShowEditGroupFormAsync(id, null);
        }

        [HttpPost("edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditGroupAsync(Group groupData)
        {
            // Check input
            if(!ModelState.IsValid)
            {
                AddStatusMessage("Ungültige Eingabe.", StatusMessageTypes.Error);
                return await ShowEditGroupFormAsync(null, groupData);
            }

            try
            {
                // Retrieve edited group from database and apply changes
                var group = await _userService.GetGroupAsync(groupData.Id, HttpContext.RequestAborted);
                group.DisplayName = groupData.DisplayName;
                group.SlotId = groupData.SlotId;
                group.ShowInScoreboard = groupData.ShowInScoreboard;
                await _userService.UpdateGroupAsync(group, HttpContext.RequestAborted);

                AddStatusMessage("Änderungen gespeichert.", StatusMessageTypes.Success);
            }
            catch(Exception ex)
            {
                AddStatusMessage("Fehler: " + ex.Message, StatusMessageTypes.Error);
                return await ShowEditGroupFormAsync(null, groupData);
            }

            return await RenderGroupListAsync();
        }

        [HttpGet("create")]
        public async Task<IActionResult> ShowCreateGroupFormAsync(Group group = null)
        {
            // Pass list of slots
            ViewData["Slots"] = await _slotService.GetSlotsAsync().ToListAsync();

            return await RenderAsync(ViewType.Create, group);
        }

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateGroupAsync(Group groupData)
        {
            // Check input
            if(!ModelState.IsValid)
            {
                AddStatusMessage("Ungültige Eingabe.", StatusMessageTypes.Error);
                return await ShowCreateGroupFormAsync(groupData);
            }

            try
            {
                // Create group
                var group = new Group
                {
                    DisplayName = groupData.DisplayName,
                    SlotId = groupData.SlotId,
                    ShowInScoreboard = groupData.ShowInScoreboard
                };
                await _userService.CreateGroupAsync(group, HttpContext.RequestAborted);

                AddStatusMessage("Die Gruppe wurde erfolgreich erstellt.", StatusMessageTypes.Success);
            }
            catch(InvalidOperationException ex)
            {
                AddStatusMessage("Fehler: " + ex.Message, StatusMessageTypes.Error);
                return await ShowCreateGroupFormAsync(groupData);
            }

            return await RenderGroupListAsync();
        }

        [HttpPost("delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteGroupAsync(int id)
        {
            // Input check
            var group = await _userService.GetGroupAsync(id, HttpContext.RequestAborted);
            if(group == null)
            {
                AddStatusMessage("Diese Gruppe existiert nicht.", StatusMessageTypes.Error);
                return await RenderGroupListAsync();
            }

            if(group.Members.Any())
            {
                AddStatusMessage("Die Gruppe muss leer sein, bevor sie gelöscht werden kann.", StatusMessageTypes.Error);
                return await RenderGroupListAsync();
            }

            try
            {
                // Delete group
                await _userService.DeleteGroupAsync(id, HttpContext.RequestAborted);

                AddStatusMessage("Die Gruppe wurde erfolgreich gelöscht.", StatusMessageTypes.Success);
            }
            catch(Exception ex)
            {
                AddStatusMessage("Fehler: " + ex, StatusMessageTypes.Error);
            }

            return await RenderGroupListAsync();
        }

        public enum ViewType
        {
            List,
            Edit,
            Create
        }
    }
}
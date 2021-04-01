using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Models;
using Ctf4e.Server.Services;
using Ctf4e.Server.Services.Sync;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Ctf4e.Server.Controllers
{
    [Route("admin/groups")]
    [Authorize(Policy = AuthenticationStrings.PolicyIsAdmin)]
    public class AdminGroupsController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IStringLocalizer<AdminGroupsController> _localizer;
        private readonly ILogger<AdminGroupsController> _logger;
        private readonly ISlotService _slotService;

        public AdminGroupsController(IUserService userService, IStringLocalizer<AdminGroupsController> localizer, ILogger<AdminGroupsController> logger, ISlotService slotService)
            : base("~/Views/AdminGroups.cshtml", userService)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                    AddStatusMessage(_localizer["ShowEditGroupFormAsync:NotFound"], StatusMessageTypes.Error);
                    return await RenderGroupListAsync();
                }
            }

            if(group == null)
            {
                AddStatusMessage(_localizer["ShowEditGroupFormAsync:MissingParameter"], StatusMessageTypes.Error);
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
                AddStatusMessage(_localizer["EditGroupAsync:InvalidInput"], StatusMessageTypes.Error);
                return await ShowEditGroupFormAsync(null, groupData);
            }

            try
            {
                // Retrieve edited group from database and apply changes
                var group = await _userService.GetGroupAsync(groupData.Id, HttpContext.RequestAborted);
                group.DisplayName = groupData.DisplayName;
                group.ScoreboardAnnotation = groupData.ScoreboardAnnotation;
                group.ScoreboardAnnotationHoverText = groupData.ScoreboardAnnotationHoverText;
                group.SlotId = groupData.SlotId;
                group.ShowInScoreboard = groupData.ShowInScoreboard;
                await _userService.UpdateGroupAsync(group, HttpContext.RequestAborted);

                AddStatusMessage(_localizer["EditGroupAsync:Success"], StatusMessageTypes.Success);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Edit group");
                AddStatusMessage(_localizer["EditGroupAsync:UnknownError"], StatusMessageTypes.Error);
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
                AddStatusMessage(_localizer["CreateGroupAsync:InvalidInput"], StatusMessageTypes.Error);
                return await ShowCreateGroupFormAsync(groupData);
            }

            try
            {
                // Create group
                var group = new Group
                {
                    DisplayName = groupData.DisplayName,
                    ScoreboardAnnotation = groupData.ScoreboardAnnotation,
                    ScoreboardAnnotationHoverText = groupData.ScoreboardAnnotationHoverText,
                    SlotId = groupData.SlotId,
                    ShowInScoreboard = groupData.ShowInScoreboard
                };
                await _userService.CreateGroupAsync(group, HttpContext.RequestAborted);

                AddStatusMessage(_localizer["CreateGroupAsync:Success"], StatusMessageTypes.Success);
            }
            catch(InvalidOperationException ex)
            {
                _logger.LogError(ex, "Create group");
                AddStatusMessage(_localizer["CreateGroupAsync:UnknownError"], StatusMessageTypes.Error);
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
                AddStatusMessage(_localizer["DeleteGroupAsync:NotFound"], StatusMessageTypes.Error);
                return await RenderGroupListAsync();
            }

            if(group.Members.Any())
            {
                AddStatusMessage(_localizer["DeleteGroupAsync:GroupNotEmpty"], StatusMessageTypes.Error);
                return await RenderGroupListAsync();
            }

            try
            {
                // Delete group
                await _userService.DeleteGroupAsync(id, HttpContext.RequestAborted);

                AddStatusMessage(_localizer["DeleteGroupAsync:Success"], StatusMessageTypes.Success);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Delete group");
                AddStatusMessage(_localizer["DeleteGroupAsync:UnknownError"], StatusMessageTypes.Error);
            }

            return await RenderGroupListAsync();
        }

        [HttpGet("sync/json")]
        public async Task<IActionResult> DownloadAsJsonAsync([FromServices] IDumpService dumpService)
        {
            try
            {
                string json = await dumpService.GetGroupDataAsync(HttpContext.RequestAborted);
                return File(Encoding.UTF8.GetBytes(json), "text/json", "groups.json");
            }
            catch(InvalidOperationException ex)
            {
                _logger.LogError(ex, "Download as JSON");
                AddStatusMessage(_localizer["DownloadAsJsonAsync:UnknownError"], StatusMessageTypes.Error);
            }

            return await RenderAsync(0, 0);
        }

        public enum ViewType
        {
            List,
            Edit,
            Create
        }
    }
}
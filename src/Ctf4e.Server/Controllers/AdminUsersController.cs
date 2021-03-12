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
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ctf4e.Server.Controllers
{
    [Route("admin/users")]
    [Authorize(Policy = AuthenticationStrings.PolicyIsAdmin)]
    public class AdminUsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IStringLocalizer<AdminUsersController> _localizer;
        private readonly ILogger<AdminUsersController> _logger;

        public AdminUsersController(IUserService userService, IOptions<MainOptions> mainOptions, IStringLocalizer<AdminUsersController> localizer, ILogger<AdminUsersController> logger)
            : base("~/Views/AdminUsers.cshtml", userService, mainOptions)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private Task<IActionResult> RenderAsync(ViewType viewType, object model)
        {
            ViewData["ViewType"] = viewType;
            return RenderViewAsync(MenuItems.AdminUsers, model);
        }

        [HttpGet]
        public async Task<IActionResult> RenderUserListAsync()
        {
            // Pass users
            var users = await _userService.GetUsersAsync().ToListAsync();

            return await RenderAsync(ViewType.List, users);
        }

        private async Task<IActionResult> ShowEditUserFormAsync(int? id, User user = null)
        {
            // Retrieve by ID, if no object from a failed POST was passed
            if(id != null)
            {
                user = await _userService.GetUserAsync(id.Value, HttpContext.RequestAborted);
                if(user == null)
                {
                    AddStatusMessage(_localizer["ShowEditUserFormAsync:NotFound"], StatusMessageTypes.Error);
                    return await RenderUserListAsync();
                }
            }

            if(user == null)
            {
                AddStatusMessage(_localizer["ShowEditUserFormAsync:MissingParameter"], StatusMessageTypes.Error);
                return await RenderUserListAsync();
            }

            // Pass list of groups
            ViewData["Groups"] = await _userService.GetGroupsAsync().ToListAsync();

            return await RenderAsync(ViewType.Edit, user);
        }

        [HttpGet("edit")]
        public Task<IActionResult> ShowEditUserFormAsync(int id)
        {
            return ShowEditUserFormAsync(id, null);
        }

        [HttpPost("edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUserAsync(User userData)
        {
            // Check input
            if(!ModelState.IsValid)
            {
                AddStatusMessage(_localizer["EditUserAsync:InvalidInput"], StatusMessageTypes.Error);
                return await ShowEditUserFormAsync(null, userData);
            }

            try
            {
                // Retrieve edited user from database and apply changes
                var user = await _userService.GetUserAsync(userData.Id, HttpContext.RequestAborted);
                user.DisplayName = userData.DisplayName;
                user.GroupFindingCode = userData.GroupFindingCode;
                user.GroupId = userData.GroupId;
                if(user.Id != (await GetCurrentUserAsync()).Id)
                    user.IsAdmin = userData.IsAdmin;
                user.IsTutor = userData.IsTutor;
                await _userService.UpdateUserAsync(user, HttpContext.RequestAborted);

                AddStatusMessage(_localizer["EditUserAsync:Success"], StatusMessageTypes.Success);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Edit user");
                AddStatusMessage(_localizer["EditUserAsync:UnknownError"], StatusMessageTypes.Error);
                return await ShowEditUserFormAsync(null, userData);
            }

            return await RenderUserListAsync();
        }

        public enum ViewType
        {
            List,
            Edit
        }
    }
}
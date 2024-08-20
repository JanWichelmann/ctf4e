using System;
using System.Linq;
using System.Threading.Tasks;
using Ctf4e.Server.Authorization;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Services;
using Ctf4e.Server.ViewModels;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Ctf4e.Server.Controllers;

[Route("admin/users")]
[AnyUserPrivilege(UserPrivileges.Admin | UserPrivileges.ViewUsers)]
public class AdminUsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IStringLocalizer<AdminUsersController> _localizer;
    private readonly ILogger<AdminUsersController> _logger;

    public AdminUsersController(IUserService userService, IStringLocalizer<AdminUsersController> localizer, ILogger<AdminUsersController> logger)
        : base("~/Views/AdminUsers.cshtml", userService)
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
        var users = await _userService.GetUsersWithGroupsAsync(HttpContext.RequestAborted);

        return await RenderAsync(ViewType.List, users);
    }

    private async Task<IActionResult> ShowEditUserFormAsync(int? id, AdminEditUserData userData = null)
    {
        // Retrieve by ID, if no object from a failed POST was passed
        if(id != null)
        {
            var user = await _userService.FindUserByIdAsync(id.Value, HttpContext.RequestAborted);
            if(user == null)
            {
                AddStatusMessage(_localizer["ShowEditUserFormAsync:NotFound"], StatusMessageTypes.Error);
                return await RenderUserListAsync();
            }

            userData = new AdminEditUserData
            {
                Id = user.Id,
                DisplayName = user.DisplayName,
                IsTutor = user.IsTutor,
                GroupFindingCode = user.GroupFindingCode,
                GroupId = user.GroupId,
                PrivilegeAdmin = user.Privileges.HasPrivileges(UserPrivileges.Admin),
                PrivilegeViewUsers = user.Privileges.HasPrivileges(UserPrivileges.ViewUsers),
                PrivilegeEditUsers = user.Privileges.HasPrivileges(UserPrivileges.EditUsers),
                PrivilegeViewGroups = user.Privileges.HasPrivileges(UserPrivileges.ViewGroups),
                PrivilegeEditGroups = user.Privileges.HasPrivileges(UserPrivileges.EditGroups),
                PrivilegeViewLabs = user.Privileges.HasPrivileges(UserPrivileges.ViewLabs),
                PrivilegeEditLabs = user.Privileges.HasPrivileges(UserPrivileges.EditLabs),
                PrivilegeViewSlots = user.Privileges.HasPrivileges(UserPrivileges.ViewSlots),
                PrivilegeEditSlots = user.Privileges.HasPrivileges(UserPrivileges.EditSlots),
                PrivilegeViewLabExecutions = user.Privileges.HasPrivileges(UserPrivileges.ViewLabExecutions),
                PrivilegeEditLabExecutions = user.Privileges.HasPrivileges(UserPrivileges.EditLabExecutions),
                PrivilegeViewAdminScoreboard = user.Privileges.HasPrivileges(UserPrivileges.ViewAdminScoreboard),
                PrivilegeEditAdminScoreboard = user.Privileges.HasPrivileges(UserPrivileges.EditAdminScoreboard),
                PrivilegeEditConfiguration = user.Privileges.HasPrivileges(UserPrivileges.EditConfiguration),
                PrivilegeTransferResults = user.Privileges.HasPrivileges(UserPrivileges.TransferResults),
                PrivilegeLoginAsLabServerAdmin = user.Privileges.HasPrivileges(UserPrivileges.LoginAsLabServerAdmin),
            };
        }

        if(userData == null)
        {
            AddStatusMessage(_localizer["ShowEditUserFormAsync:MissingParameter"], StatusMessageTypes.Error);
            return await RenderUserListAsync();
        }

        // Pass list of groups
        ViewData["Groups"] = await _userService.GetGroupsAsync(HttpContext.RequestAborted);

        return await RenderAsync(ViewType.Edit, userData);
    }

    [HttpGet("edit")]
    [AnyUserPrivilege(UserPrivileges.Admin | UserPrivileges.EditUsers)]
    public Task<IActionResult> ShowEditUserFormAsync(int id)
    {
        return ShowEditUserFormAsync(id, null);
    }

    [HttpPost("edit")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.Admin | UserPrivileges.EditUsers)]
    public async Task<IActionResult> EditUserAsync(AdminEditUserData userData)
    {
        // Check input
        if(!ModelState.IsValid)
        {
            AddStatusMessage(_localizer["EditUserAsync:InvalidInput"], StatusMessageTypes.Error);
            return await ShowEditUserFormAsync(null, userData);
        }

        var currentUser = await GetCurrentUserAsync();
        try
        {
            // Retrieve edited user from database and apply changes
            var user = await _userService.FindUserByIdAsync(userData.Id, HttpContext.RequestAborted);
            user.DisplayName = userData.DisplayName;
            user.IsTutor = userData.IsTutor;
            user.GroupFindingCode = userData.GroupFindingCode;
            user.GroupId = userData.GroupId;

            if(currentUser.Privileges.HasPrivileges(UserPrivileges.Admin))
            {
                // Privileges
                var privileges = UserPrivileges.Default;
                if(userData.PrivilegeAdmin)
                    privileges |= UserPrivileges.Admin;
                if(userData.PrivilegeViewUsers)
                    privileges |= UserPrivileges.ViewUsers;
                if(userData.PrivilegeEditUsers)
                    privileges |= UserPrivileges.ViewUsers | UserPrivileges.EditUsers;
                if(userData.PrivilegeViewGroups)
                    privileges |= UserPrivileges.ViewGroups;
                if(userData.PrivilegeEditGroups)
                    privileges |= UserPrivileges.ViewGroups | UserPrivileges.EditGroups;
                if(userData.PrivilegeViewLabs)
                    privileges |= UserPrivileges.ViewLabs;
                if(userData.PrivilegeEditLabs)
                    privileges |= UserPrivileges.ViewLabs | UserPrivileges.EditLabs;
                if(userData.PrivilegeViewSlots)
                    privileges |= UserPrivileges.ViewSlots;
                if(userData.PrivilegeEditSlots)
                    privileges |= UserPrivileges.ViewSlots | UserPrivileges.EditSlots;
                if(userData.PrivilegeViewLabExecutions)
                    privileges |= UserPrivileges.ViewLabExecutions;
                if(userData.PrivilegeEditLabExecutions)
                    privileges |= UserPrivileges.ViewLabExecutions | UserPrivileges.EditLabExecutions;
                if(userData.PrivilegeViewAdminScoreboard)
                    privileges |= UserPrivileges.ViewAdminScoreboard;
                if(userData.PrivilegeEditAdminScoreboard)
                    privileges |= UserPrivileges.ViewAdminScoreboard | UserPrivileges.EditAdminScoreboard;
                if(userData.PrivilegeEditConfiguration)
                    privileges |= UserPrivileges.EditConfiguration;
                if(userData.PrivilegeTransferResults)
                    privileges |= UserPrivileges.TransferResults;
                if(userData.PrivilegeLoginAsLabServerAdmin)
                    privileges |= UserPrivileges.LoginAsLabServerAdmin;

                // Ensure that admins don't accidentally lock out themselves
                if(user.Id == currentUser.Id)
                {
                    privileges |= UserPrivileges.Admin;
                }

                user.Privileges = privileges;
            }

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
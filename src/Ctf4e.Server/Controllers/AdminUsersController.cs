using System;
using System.Threading.Tasks;
using Ctf4e.Server.Authorization;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Services;
using Ctf4e.Server.ViewModels;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ctf4e.Server.Controllers;

[Route("admin/users")]
[AnyUserPrivilege(UserPrivileges.Admin | UserPrivileges.ViewUsers)]
public class AdminUsersController(IUserService userService)
    : ControllerBase<AdminUsersController>(userService)
{
    protected override MenuItems ActiveMenuItem => MenuItems.AdminUsers;
    
    private readonly IUserService _userService = userService;

    private Task<IActionResult> RenderAsync(ViewType viewType, string viewPath, object model)
    {
        ViewData["ViewType"] = viewType;
        return RenderViewAsync(viewPath, model);
    }

    [HttpGet]
    public async Task<IActionResult> RenderUserListAsync()
    {
        // Pass users
        var users = await _userService.GetUsersWithGroupsAsync(HttpContext.RequestAborted);

        return await RenderAsync(ViewType.List, "~/Views/AdminUsers.cshtml", users);
    }

    private async Task<IActionResult> ShowEditUserFormAsync(int? id, AdminEditUserData userData = null)
    {
        // Retrieve by ID, if no object from a failed POST was passed
        if(id != null)
        {
            var user = await _userService.FindUserByIdAsync(id.Value, HttpContext.RequestAborted);
            if(user == null)
            {
                AddStatusMessage(StatusMessageType.Error, Localizer["ShowEditUserFormAsync:NotFound"]);
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
            AddStatusMessage(StatusMessageType.Error, Localizer["ShowEditUserFormAsync:MissingParameter"]);
            return await RenderUserListAsync();
        }

        // Pass list of groups
        var groupService = HttpContext.RequestServices.GetRequiredService<IGroupService>();
        ViewData["Groups"] = await groupService.GetGroupsAsync(HttpContext.RequestAborted);

        return await RenderAsync(ViewType.Edit, "~/Views/AdminUsers.cshtml", userData);
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
            AddStatusMessage(StatusMessageType.Error, Localizer["EditUserAsync:InvalidInput"]);
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

            AddStatusMessage(StatusMessageType.Success, Localizer["EditUserAsync:Success"]);
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Edit user");
            AddStatusMessage(StatusMessageType.Error, Localizer["EditUserAsync:UnknownError"]);
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
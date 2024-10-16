using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ctf4e.Server.Authorization;
using Ctf4e.Server.Constants;
using Ctf4e.Server.InputModels;
using Ctf4e.Server.Services;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Http;
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

    [HttpGet]
    public async Task<IActionResult> RenderUserListAsync()
    {
        // Pass users
        var users = await _userService.GetUserListAsync(HttpContext.RequestAborted);

        return await RenderViewAsync("~/Views/Admin/Users/Index.cshtml", users);
    }

    private async Task<IActionResult> ShowEditUserFormAsync(AdminUserInputModel userInput)
    {
        // Pass list of groups
        var groupService = HttpContext.RequestServices.GetRequiredService<IGroupService>();
        ViewData["Groups"] = await groupService.GetGroupsAsync(HttpContext.RequestAborted);

        return await RenderViewAsync("~/Views/Admin/Users/Edit.cshtml", userInput);
    }

    [HttpGet("edit")]
    [AnyUserPrivilege(UserPrivileges.Admin | UserPrivileges.EditUsers)]
    public async Task<IActionResult> ShowEditUserFormAsync(int id)
    {
        var user = await _userService.FindUserByIdAsync(id, HttpContext.RequestAborted);
        if(user == null)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["ShowEditUserFormAsync:NotFound"]);
            return await RenderUserListAsync();
        }

        var userInputModel = new AdminUserInputModel
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            IsTutor = user.IsTutor,
            GroupFindingCode = user.GroupFindingCode,
            LabUserName = user.LabUserName,
            LabPassword = user.LabPassword,
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

        return await ShowEditUserFormAsync(userInputModel);
    }


    [HttpPost("edit")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.Admin | UserPrivileges.EditUsers)]
    public async Task<IActionResult> EditUserAsync(AdminUserInputModel userInput)
    {
        // Check input
        if(!ModelState.IsValid)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["EditUserAsync:InvalidInput"]);
            return await ShowEditUserFormAsync(userInput);
        }

        var currentUser = await GetCurrentUserAsync();
        try
        {
            // Retrieve edited user from database and apply changes
            var user = await _userService.FindUserByIdAsync(userInput.Id, HttpContext.RequestAborted);
            if(user == null)
            {
                PostStatusMessage = new(StatusMessageType.Error, Localizer["EditUserAsync:NotFound"]);
                return RedirectToAction("RenderUserList");
            }

            user.DisplayName = userInput.DisplayName;
            user.IsTutor = userInput.IsTutor;
            user.GroupFindingCode = userInput.GroupFindingCode;
            user.LabUserName = userInput.LabUserName;
            user.LabPassword = userInput.LabPassword;
            user.GroupId = userInput.GroupId;

            if(currentUser.Privileges.HasPrivileges(UserPrivileges.Admin))
            {
                // Privileges
                var privileges = UserPrivileges.Default;
                if(userInput.PrivilegeAdmin)
                    privileges |= UserPrivileges.Admin;
                if(userInput.PrivilegeViewUsers)
                    privileges |= UserPrivileges.ViewUsers;
                if(userInput.PrivilegeEditUsers)
                    privileges |= UserPrivileges.ViewUsers | UserPrivileges.EditUsers;
                if(userInput.PrivilegeViewGroups)
                    privileges |= UserPrivileges.ViewGroups;
                if(userInput.PrivilegeEditGroups)
                    privileges |= UserPrivileges.ViewGroups | UserPrivileges.EditGroups;
                if(userInput.PrivilegeViewLabs)
                    privileges |= UserPrivileges.ViewLabs;
                if(userInput.PrivilegeEditLabs)
                    privileges |= UserPrivileges.ViewLabs | UserPrivileges.EditLabs;
                if(userInput.PrivilegeViewSlots)
                    privileges |= UserPrivileges.ViewSlots;
                if(userInput.PrivilegeEditSlots)
                    privileges |= UserPrivileges.ViewSlots | UserPrivileges.EditSlots;
                if(userInput.PrivilegeViewLabExecutions)
                    privileges |= UserPrivileges.ViewLabExecutions;
                if(userInput.PrivilegeEditLabExecutions)
                    privileges |= UserPrivileges.ViewLabExecutions | UserPrivileges.EditLabExecutions;
                if(userInput.PrivilegeViewAdminScoreboard)
                    privileges |= UserPrivileges.ViewAdminScoreboard;
                if(userInput.PrivilegeEditAdminScoreboard)
                    privileges |= UserPrivileges.ViewAdminScoreboard | UserPrivileges.EditAdminScoreboard;
                if(userInput.PrivilegeEditConfiguration)
                    privileges |= UserPrivileges.EditConfiguration;
                if(userInput.PrivilegeTransferResults)
                    privileges |= UserPrivileges.TransferResults;
                if(userInput.PrivilegeLoginAsLabServerAdmin)
                    privileges |= UserPrivileges.LoginAsLabServerAdmin;

                // Ensure that admins don't accidentally lock out themselves
                if(user.Id == currentUser.Id)
                {
                    privileges |= UserPrivileges.Admin;
                }

                user.Privileges = privileges;
            }

            await _userService.UpdateUserAsync(user, HttpContext.RequestAborted);

            PostStatusMessage = new(StatusMessageType.Success, Localizer["EditUserAsync:Success"]) { AutoHide = true };
            return RedirectToAction("RenderUserList");
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Edit user");
            AddStatusMessage(StatusMessageType.Error, Localizer["EditUserAsync:UnknownError"]);
            return await ShowEditUserFormAsync(userInput);
        }
    }
    
    [HttpGet("credentials/export")]
    public async Task<IActionResult> ExportLabCredentialsAsync()
    {
        try
        {
            var users = await _userService.GetUsersAsync(HttpContext.RequestAborted);
            StringBuilder data = new();
            data.AppendLine("ID\tUsername\tPassword");
            foreach(var u in users)
                data.AppendLine($"{u.Id}\t{u.LabUserName}\t{u.LabPassword}");
            
            return File(Encoding.UTF8.GetBytes(data.ToString()), "text/csv", "user-credentials.csv");
        }
        catch(InvalidOperationException ex)
        {
            GetLogger().LogError(ex, "Export lab credentials");
            PostStatusMessage = new StatusMessage(StatusMessageType.Error, Localizer["ExportLabCredentialsAsync:UnknownError"]);
            return RedirectToAction("RenderUserList");
        }
    }

    [HttpPost("credentials/import")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportLabCredentialsAsync(IFormFile credentialsFile)
    {
        if(credentialsFile == null)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["ImportLabCredentialsAsync:NoFile"]);
            return await RenderUserListAsync();
        }
        
        // Parse file contents as CSV
        // We always export with tabs and enforce this here as well
        try
        {
            var users = await _userService.GetUsersAsync(HttpContext.RequestAborted);
            var userLookup = users.ToDictionary(u => u.Id);
            
            using var fileReader = new StreamReader(credentialsFile.OpenReadStream());
            
            string line;
            bool error = false;
            bool firstLine = true;
            while((line = await fileReader.ReadLineAsync()) != null)
            {
                var parts = line.Split('\t');
                if(parts.Length != 3)
                {
                    AddStatusMessage(StatusMessageType.Error, Localizer["ImportLabCredentialsAsync:InvalidFormat"]);
                    return await RenderUserListAsync();
                }

                if(!int.TryParse(parts[0], out int userId) || !userLookup.TryGetValue(userId, out var user))
                {
                    // Skip optional header line
                    if(firstLine)
                    {
                        firstLine = false;
                        continue;
                    }

                    AddStatusMessage(StatusMessageType.Error, Localizer["ImportLabCredentialsAsync:InvalidUserId", parts[0]]);
                    error = true;
                    continue;
                }

                user.LabUserName = string.IsNullOrWhiteSpace(parts[1]) ? null : parts[1];
                user.LabPassword = string.IsNullOrWhiteSpace(parts[2]) ? null : parts[2];
                await _userService.UpdateUserAsync(user, HttpContext.RequestAborted);
                
                firstLine = false;
            }
            
            if(error)
                return await RenderUserListAsync();

            PostStatusMessage = new StatusMessage(StatusMessageType.Success, Localizer["ImportLabCredentialsAsync:Success"]) { AutoHide = true };
            return RedirectToAction("RenderUserList");
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Import lab credentials");
            AddStatusMessage(StatusMessageType.Error, Localizer["ImportLabCredentialsAsync:UnknownError"]);
            return await RenderUserListAsync();
        }
    }    
}
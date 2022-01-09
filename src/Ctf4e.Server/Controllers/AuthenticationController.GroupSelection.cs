using System;
using System.Linq;
using System.Threading.Tasks;
using Ctf4e.Server.Models;
using Ctf4e.Server.ViewModels;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Ctf4e.Server.Controllers;

public partial class AuthenticationController
{
    private async Task<IActionResult> ShowGroupFormAsync(GroupSelection groupSelection)
    {
        // Pass slots
        ViewData["Slots"] = await _slotService.GetSlotsAsync().ToListAsync();

        return await RenderAsync(ViewType.GroupSelection, groupSelection);
    }

    [HttpGet("selgroup")]
    [Authorize]
    public Task<IActionResult> ShowGroupFormAsync()
    {
        return ShowGroupFormAsync(new GroupSelection { ShowInScoreboard = true });
    }

    [HttpPost("selgroup")]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> HandleGroupSelectionAsync(GroupSelection groupSelection)
    {
        // Some input validation
        if(!ModelState.IsValid)
        {
            AddStatusMessage(_localizer["HandleGroupSelectionAsync:InvalidInput"], StatusMessageTypes.Error);
            return await ShowGroupFormAsync(groupSelection);
        }

        // Does the user already have a group?
        var currentUser = await GetCurrentUserAsync();
        if(currentUser.Group != null)
            return await ShowRedirectAsync(null);

        // Try to create group
        try
        {
            // Filter group codes
            var groupSizeMin = await _configurationService.GetGroupSizeMinAsync(HttpContext.RequestAborted);
            var groupSizeMax = await _configurationService.GetGroupSizeMaxAsync(HttpContext.RequestAborted);
            var codes = groupSelection.OtherUserCodes
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Append(currentUser.GroupFindingCode)
                .Select(c => c.Trim())
                .Distinct()
                .ToList();
            if(codes.Count < groupSizeMin)
            {
                AddStatusMessage(_localizer["HandleGroupSelectionAsync:TooFewCodes", groupSizeMin], StatusMessageTypes.Error);
                return await ShowGroupFormAsync(groupSelection);
            }

            if(codes.Count > groupSizeMax)
            {
                AddStatusMessage(_localizer["HandleGroupSelectionAsync:TooManyCodes", groupSizeMax], StatusMessageTypes.Error);
                return await ShowGroupFormAsync(groupSelection);
            }

            // Create group
            // The service method will do further error checking (i.e., validity of codes, whether users are already in a group, ...)
            var group = new Group
            {
                DisplayName = groupSelection.DisplayName,
                SlotId = groupSelection.SlotId,
                ShowInScoreboard = groupSelection.ShowInScoreboard
            };
            await _userService.CreateGroupAsync(group, codes, HttpContext.RequestAborted);
        }
        catch(ArgumentException)
        {
            AddStatusMessage(_localizer["HandleGroupSelectionAsync:InvalidInput"], StatusMessageTypes.Error);
            return await ShowGroupFormAsync(groupSelection);
        }
        catch(InvalidOperationException)
        {
            AddStatusMessage(_localizer["HandleGroupSelectionAsync:CodeAlreadyAssigned"], StatusMessageTypes.Error);
            return await ShowGroupFormAsync(groupSelection);
        }
        catch(Exception ex)
        {
            // Should only happen on larger database failures or when users mess around with the input model
            _logger.LogError(ex, "Create group");
            AddStatusMessage(_localizer["HandleGroupSelectionAsync:UnknownError"], StatusMessageTypes.Error);
            return await ShowGroupFormAsync(groupSelection);
        }

        // Success
        AddStatusMessage(_localizer["HandleGroupSelectionAsync:Success"], StatusMessageTypes.Success);
        return await ShowRedirectAsync(null);
    }
}
using System;
using System.Linq;
using System.Threading.Tasks;
using Ctf4e.Server.Models;
using Ctf4e.Server.Services;
using Ctf4e.Server.ViewModels;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ctf4e.Server.Controllers;

public partial class AuthenticationController
{
    private async Task<IActionResult> ShowGroupFormAsync(GroupSelection groupSelection)
    {
        // Pass slots
        var slotService = HttpContext.RequestServices.GetRequiredService<ISlotService>();
        ViewData["Slots"] = await slotService.GetSlotsAsync(HttpContext.RequestAborted);

        return await RenderViewAsync("~/Views/Authentication/SelectGroup.cshtml", groupSelection);
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
    public async Task<IActionResult> HandleGroupSelectionAsync(GroupSelection groupSelection,
                                                               [FromServices] ILabExecutionService labExecutionService,
                                                               [FromServices] IGroupService groupService,
                                                               [FromServices] ISlotService slotService)
    {
        // Some input validation
        if(!ModelState.IsValid)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["HandleGroupSelectionAsync:InvalidInput"]);
            return await ShowGroupFormAsync(groupSelection);
        }

        // Does the user already have a group?
        var currentUser = await GetCurrentUserAsync();
        if(currentUser.Group != null)
            return await RedirectAsync(null);

        // Try to create group
        int groupId;
        try
        {
            // Filter group codes
            var groupSizeMin = await configurationService.GroupSizeMin.GetAsync(HttpContext.RequestAborted);
            var groupSizeMax = await configurationService.GroupSizeMax.GetAsync(HttpContext.RequestAborted);
            var codes = groupSelection.OtherUserCodes
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Append(currentUser.GroupFindingCode)
                .Select(c => c.Trim())
                .Distinct()
                .ToList();
            if(codes.Count < groupSizeMin)
            {
                AddStatusMessage(StatusMessageType.Error, Localizer["HandleGroupSelectionAsync:TooFewCodes", groupSizeMin]);
                return await ShowGroupFormAsync(groupSelection);
            }

            if(codes.Count > groupSizeMax)
            {
                AddStatusMessage(StatusMessageType.Error, Localizer["HandleGroupSelectionAsync:TooManyCodes", groupSizeMax]);
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
            groupId = await groupService.CreateGroupFromCodesAsync(group, codes, HttpContext.RequestAborted);
        }
        catch(ArgumentException)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["HandleGroupSelectionAsync:InvalidInput"]);
            return await ShowGroupFormAsync(groupSelection);
        }
        catch(InvalidOperationException)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["HandleGroupSelectionAsync:CodeAlreadyAssigned"]);
            return await ShowGroupFormAsync(groupSelection);
        }
        catch(Exception ex)
        {
            // Should only happen on larger database failures or when users mess around with the input model
            GetLogger().LogError(ex, "Create group");
            AddStatusMessage(StatusMessageType.Error, Localizer["HandleGroupSelectionAsync:UnknownError"]);
            return await ShowGroupFormAsync(groupSelection);
        }

        try
        {
            // Start default lab if specified in the slot configuration
            var slot = await slotService.FindSlotByIdAsync(groupSelection.SlotId, HttpContext.RequestAborted);
            if(slot is { DefaultExecuteLabId: { }, DefaultExecuteLabEnd: { } })
            {
                var startTime = DateTime.Now;
                if(startTime < slot.DefaultExecuteLabEnd)
                {
                    var labExecution = new LabExecution
                    {
                        GroupId = groupId,
                        LabId = slot.DefaultExecuteLabId.Value,
                        Start = startTime,
                        End = slot.DefaultExecuteLabEnd.Value
                    };
                    await labExecutionService.CreateLabExecutionAsync(labExecution, false, HttpContext.RequestAborted);
                }
            }
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Start default lab on group creation");
            PostStatusMessage = new StatusMessage(StatusMessageType.Error, Localizer["HandleGroupSelectionAsync:DefaultLabStartError"]);
            return await RedirectAsync(null);
        }

        // Success
        PostStatusMessage = new StatusMessage(StatusMessageType.Success, Localizer["HandleGroupSelectionAsync:Success"]);
        return await RedirectAsync(null);
    }
}
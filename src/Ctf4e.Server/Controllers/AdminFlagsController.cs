using System;
using System.Threading.Tasks;
using Ctf4e.Server.Authorization;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Models;
using Ctf4e.Server.Services;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ctf4e.Server.Controllers;

[Route("admin/flags")]
[AnyUserPrivilege(UserPrivileges.ViewLabs)]
public class AdminFlagsController(IUserService userService, IFlagService flagService)
    : ControllerBase<AdminFlagsController>(userService)
{
    protected override MenuItems ActiveMenuItem => MenuItems.AdminFlags;

    private async Task<IActionResult> RenderAsync(ViewType viewType, string viewPath, int labId, object model)
    {
        var labService = HttpContext.RequestServices.GetRequiredService<ILabService>();
        var lab = await labService.FindLabByIdAsync(labId, HttpContext.RequestAborted);
        if(lab == null)
            return RedirectToAction("RenderLabList", "AdminLabs");
        ViewData["Lab"] = lab;

        ViewData["ViewType"] = viewType;
        return await RenderViewAsync(viewPath, model);
    }

    [HttpGet]
    public async Task<IActionResult> RenderFlagListAsync(int labId)
    {
        var flags = await flagService.GetFlagsAsync(labId, HttpContext.RequestAborted);

        return await RenderAsync(ViewType.List, "~/Views/AdminFlags.cshtml", labId, flags);
    }

    private async Task<IActionResult> ShowEditFlagFormAsync(int? id, Flag flag = null)
    {
        // Retrieve by ID, if no object from a failed POST was passed
        if(id != null)
        {
            flag = await flagService.FindFlagByIdAsync(id.Value, HttpContext.RequestAborted);
            if(flag == null)
                return RedirectToAction("RenderLabList", "AdminLabs");
        }

        if(flag == null)
            return RedirectToAction("RenderLabList", "AdminLabs");

        return await RenderAsync(ViewType.Edit, "~/Views/AdminFlags.cshtml", flag.LabId, flag);
    }

    [HttpGet("edit")]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public Task<IActionResult> ShowEditFlagFormAsync(int id)
    {
        return ShowEditFlagFormAsync(id, null);
    }

    [HttpPost("edit")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public async Task<IActionResult> EditFlagAsync(Flag flagData)
    {
        // Check input
        if(!ModelState.IsValid)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["EditFlagAsync:InvalidInput"]);
            return await ShowEditFlagFormAsync(null, flagData);
        }

        try
        {
            // Retrieve edited flag from database and apply changes
            var flag = await flagService.FindFlagByIdAsync(flagData.Id, HttpContext.RequestAborted);
            flag.Code = flagData.Code;
            flag.Description = flagData.Description;
            flag.BasePoints = flagData.BasePoints;
            flag.IsBounty = flagData.IsBounty;
            await flagService.UpdateFlagAsync(flag, HttpContext.RequestAborted);

            AddStatusMessage(StatusMessageType.Success, Localizer["EditFlagAsync:Success"]);
            return await RenderFlagListAsync(flag.LabId);
        }
        catch(InvalidOperationException ex)
        {
            GetLogger().LogError(ex, "Edit flag");
            AddStatusMessage(StatusMessageType.Error, Localizer["EditFlagAsync:UnknownError"]);
            return await ShowEditFlagFormAsync(null, flagData);
        }
    }

    [HttpGet("create")]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public async Task<IActionResult> ShowCreateFlagFormAsync(int labId, Flag flag = null)
    {
        return await RenderAsync(ViewType.Create, "~/Views/AdminFlags.cshtml", labId, flag);
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public async Task<IActionResult> CreateFlagAsync(Flag flagData, string returnToForm)
    {
        // Check input
        if(!ModelState.IsValid)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateFlagAsync:InvalidInput"]);
            return await ShowCreateFlagFormAsync(flagData.LabId, flagData);
        }

        try
        {
            // Create flag
            var flag = new Flag
            {
                Code = flagData.Code,
                Description = flagData.Description,
                BasePoints = flagData.BasePoints,
                IsBounty = flagData.IsBounty,
                LabId = flagData.LabId
            };
            await flagService.CreateFlagAsync(flag, HttpContext.RequestAborted);

            AddStatusMessage(StatusMessageType.Success, Localizer["CreateFlagAsync:Success"]);
                
            if(!string.IsNullOrEmpty(returnToForm))
                return await ShowCreateFlagFormAsync(flagData.LabId, flagData);
            return await RenderFlagListAsync(flagData.LabId);
        }
        catch(InvalidOperationException ex)
        {
            GetLogger().LogError(ex, "Create flag");
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateFlagAsync:UnknownError"]);
            return await ShowCreateFlagFormAsync(flagData.LabId, flagData);
        }
    }

    [HttpPost("delete")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public async Task<IActionResult> DeleteFlagAsync(int id)
    {
        // Input check
        var flag = await flagService.FindFlagByIdAsync(id, HttpContext.RequestAborted);
        if(flag == null)
            return RedirectToAction("RenderLabList", "AdminLabs");

        try
        {
            // Delete flag
            await flagService.DeleteFlagAsync(id, HttpContext.RequestAborted);

            AddStatusMessage(StatusMessageType.Success, Localizer["DeleteFlagAsync:Success"]);
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Delete flag");
            AddStatusMessage(StatusMessageType.Error, Localizer["DeleteFlagAsync:UnknownError"]);
        }

        return await RenderFlagListAsync(flag.LabId);
    }

    public enum ViewType
    {
        List,
        Edit,
        Create
    }
}
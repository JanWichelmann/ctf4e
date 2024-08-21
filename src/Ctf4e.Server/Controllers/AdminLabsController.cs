using System;
using System.Threading.Tasks;
using Ctf4e.Server.Authorization;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Models;
using Ctf4e.Server.Services;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Ctf4e.Server.Controllers;

[Route("admin/labs")]
[AnyUserPrivilege(UserPrivileges.ViewLabs)]
public class AdminLabsController(IUserService userService, ILabService labService)
    : ControllerBase<AdminLabsController>(userService)
{
    protected override MenuItems ActiveMenuItem => MenuItems.AdminLabs;

    private Task<IActionResult> RenderAsync(ViewType viewType, string viewPath, object model)
    {
        ViewData["ViewType"] = viewType;

        return RenderViewAsync(viewPath, model);
    }

    [HttpGet]
    public async Task<IActionResult> RenderLabListAsync()
    {
        // Pass labs
        var labs = await labService.GetFullLabsAsync(HttpContext.RequestAborted);

        return await RenderAsync(ViewType.List, "~/Views/AdminLabs.cshtml", labs);
    }

    private async Task<IActionResult> ShowEditLabFormAsync(int? id, Lab lab = null)
    {
        // Retrieve by ID, if no object from a failed POST was passed
        if(id != null)
        {
            lab = await labService.FindLabByIdAsync(id.Value, HttpContext.RequestAborted);
            if(lab == null)
            {
                AddStatusMessage(StatusMessageType.Error, Localizer["ShowEditLabFormAsync:NotFound"]);
                return await RenderLabListAsync();
            }
        }

        if(lab == null)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["ShowEditLabFormAsync:MissingParameter"]);
            return await RenderLabListAsync();
        }

        return await RenderAsync(ViewType.Edit, "~/Views/AdminLabs.cshtml", lab);
    }

    [HttpGet("edit")]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public Task<IActionResult> ShowEditLabFormAsync(int id)
    {
        return ShowEditLabFormAsync(id, null);
    }

    [HttpPost("edit")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public async Task<IActionResult> EditLabAsync(Lab labData)
    {
        // Check input
        if(!ModelState.IsValid)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["EditLabAsync:InvalidInput"]);
            return await ShowEditLabFormAsync(null, labData);
        }

        try
        {
            // Retrieve edited lab from database and apply changes
            var lab = await labService.FindLabByIdAsync(labData.Id, HttpContext.RequestAborted);
            lab.Name = labData.Name;
            lab.ApiCode = labData.ApiCode;
            lab.ServerBaseUrl = labData.ServerBaseUrl;
            lab.MaxPoints = labData.MaxPoints;
            lab.MaxFlagPoints = labData.MaxFlagPoints;
            lab.Visible = labData.Visible;
            await labService.UpdateLabAsync(lab, HttpContext.RequestAborted);

            AddStatusMessage(StatusMessageType.Success, Localizer["EditLabAsync:Success"]);
        }
        catch(InvalidOperationException ex)
        {
            GetLogger().LogError(ex, "Edit lab");
            AddStatusMessage(StatusMessageType.Error, Localizer["EditLabAsync:UnknownError"]);
            return await ShowEditLabFormAsync(null, labData);
        }

        return await RenderLabListAsync();
    }

    [HttpGet("create")]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public async Task<IActionResult> ShowCreateLabFormAsync(Lab lab = null)
    {
        return await RenderAsync(ViewType.Create, "~/Views/AdminLabs.cshtml", lab);
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public async Task<IActionResult> CreateLabAsync(Lab labData)
    {
        // Check input
        if(!ModelState.IsValid)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateLabAsync:InvalidInput"]);
            return await ShowCreateLabFormAsync(labData);
        }

        try
        {
            // Create lab
            var lab = new Lab
            {
                Name = labData.Name,
                ApiCode = labData.ApiCode,
                ServerBaseUrl = labData.ServerBaseUrl,
                MaxPoints = labData.MaxPoints,
                MaxFlagPoints = labData.MaxFlagPoints,
                Visible = labData.Visible
            };
            await labService.CreateLabAsync(lab, HttpContext.RequestAborted);

            AddStatusMessage(StatusMessageType.Success, Localizer["CreateLabAsync:Success"]);
        }
        catch(InvalidOperationException ex)
        {
            GetLogger().LogError(ex, "Create lab");
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateLabAsync:UnknownError"]);
            return await ShowCreateLabFormAsync(labData);
        }

        return await RenderLabListAsync();
    }

    [HttpPost("delete")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public async Task<IActionResult> DeleteLabAsync(int id, [FromServices] ILabExecutionService labExecutionService)
    {
        // Input check
        var lab = await labService.FindLabByIdAsync(id, HttpContext.RequestAborted);
        if(lab == null)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["DeleteLabAsync:NotFound"]);
            return await RenderLabListAsync();
        }

        if(await labExecutionService.AnyLabExecutionsForLabAsync(id, HttpContext.RequestAborted))
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["DeleteLabAsync:HasExecutions"]);
            return await RenderLabListAsync();
        }

        try
        {
            // Delete lab
            await labService.DeleteLabAsync(id, HttpContext.RequestAborted);

            AddStatusMessage(StatusMessageType.Success, Localizer["DeleteLabAsync:Success"]);
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Delete lab");
            AddStatusMessage(StatusMessageType.Error, Localizer["DeleteLabAsync:UnknownError"]);
        }

        return await RenderLabListAsync();
    }

    public enum ViewType
    {
        List,
        Edit,
        Create
    }
}
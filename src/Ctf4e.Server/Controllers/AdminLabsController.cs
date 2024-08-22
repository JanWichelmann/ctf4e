using System;
using System.Threading.Tasks;
using AutoMapper;
using Ctf4e.Server.Authorization;
using Ctf4e.Server.Constants;
using Ctf4e.Server.InputModels;
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

    [HttpGet]
    public async Task<IActionResult> RenderLabListAsync()
    {
        // Pass labs
        var labs = await labService.GetLabListAsync(HttpContext.RequestAborted);

        return await RenderViewAsync("~/Views/Admin/Labs/Index.cshtml", labs);
    }

    private async Task<IActionResult> ShowEditLabFormAsync(AdminLabInputModel labInput)
    {
        return await RenderViewAsync("~/Views/Admin/Labs/Edit.cshtml", labInput);
    }

    [HttpGet("edit")]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public async Task<IActionResult> ShowEditLabFormAsync(int id, [FromServices] IMapper mapper)
    {
        var lab = await labService.FindLabByIdAsync(id, HttpContext.RequestAborted);
        if(lab == null)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["ShowEditLabFormAsync:NotFound"]);
            return await RenderLabListAsync();
        }

        var labData = mapper.Map<AdminLabInputModel>(lab);
        return await ShowEditLabFormAsync(labData);
    }

    [HttpPost("edit")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public async Task<IActionResult> EditLabAsync(AdminLabInputModel labInput, [FromServices] IMapper mapper)
    {
        // Check input
        if(!ModelState.IsValid || labInput.Id == null)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["EditLabAsync:InvalidInput"]);
            return await ShowEditLabFormAsync(labInput);
        }

        try
        {
            // Retrieve edited lab from database and apply changes
            var lab = await labService.FindLabByIdAsync(labInput.Id.Value, HttpContext.RequestAborted);
            if(lab == null)
            {
                PostStatusMessage = new(StatusMessageType.Error, Localizer["EditLabAsync:NotFound"]) { AutoHide = true };
                return RedirectToAction("RenderLabList");
            }

            mapper.Map(labInput, lab);

            await labService.UpdateLabAsync(lab, HttpContext.RequestAborted);

            PostStatusMessage = new(StatusMessageType.Success, Localizer["EditLabAsync:Success"]) { AutoHide = true };
            return RedirectToAction("RenderLabList");
        }
        catch(InvalidOperationException ex)
        {
            GetLogger().LogError(ex, "Edit lab");
            AddStatusMessage(StatusMessageType.Error, Localizer["EditLabAsync:UnknownError"]);
            return await ShowEditLabFormAsync(labInput);
        }
    }

    private async Task<IActionResult> ShowCreateLabFormAsync(AdminLabInputModel labInput)
    {
        return await RenderViewAsync("~/Views/Admin/Labs/Create.cshtml", labInput);
    }

    [HttpGet("create")]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public Task<IActionResult> ShowCreateLabFormAsync()
        => ShowCreateLabFormAsync(null);

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public async Task<IActionResult> CreateLabAsync(AdminLabInputModel labInput, [FromServices] IMapper mapper)
    {
        // Check input
        if(!ModelState.IsValid)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateLabAsync:InvalidInput"]);
            return await ShowCreateLabFormAsync(labInput);
        }

        try
        {
            // Create lab
            var lab = mapper.Map<Lab>(labInput);
            await labService.CreateLabAsync(lab, HttpContext.RequestAborted);

            PostStatusMessage = new(StatusMessageType.Success, Localizer["CreateLabAsync:Success"]) { AutoHide = true };
            return RedirectToAction("RenderLabList");
        }
        catch(InvalidOperationException ex)
        {
            GetLogger().LogError(ex, "Create lab");
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateLabAsync:UnknownError"]);
            return await ShowCreateLabFormAsync(labInput);
        }
    }

    [HttpPost("delete")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public async Task<IActionResult> DeleteLabAsync(int id, [FromServices] ILabExecutionService labExecutionService)
    {
        try
        {
            // Input check
            var lab = await labService.FindLabByIdAsync(id, HttpContext.RequestAborted);
            if(lab == null)
            {
                PostStatusMessage = new(StatusMessageType.Error, Localizer["DeleteLabAsync:NotFound"]);
                return RedirectToAction("RenderLabList");
            }

            if(await labExecutionService.AnyLabExecutionsForLabAsync(id, HttpContext.RequestAborted))
            {
                PostStatusMessage = new(StatusMessageType.Error, Localizer["DeleteLabAsync:HasExecutions"]);
                return RedirectToAction("RenderLabList");
            }

            // Delete lab
            await labService.DeleteLabAsync(id, HttpContext.RequestAborted);

            PostStatusMessage = new(StatusMessageType.Success, Localizer["DeleteLabAsync:Success"]) { AutoHide = true };
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Delete lab");
            PostStatusMessage = new(StatusMessageType.Error, Localizer["DeleteLabAsync:UnknownError"]);
        }

        return RedirectToAction("RenderLabList");
    }

    public static void RegisterMappings(Profile mappingProfile)
    {
        mappingProfile.CreateMap<Lab, AdminLabInputModel>();
        mappingProfile.CreateMap<AdminLabInputModel, Lab>();
    }
}
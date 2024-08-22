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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ctf4e.Server.Controllers;

[Route("admin/flags")]
[AnyUserPrivilege(UserPrivileges.ViewLabs)]
public class AdminFlagsController(IUserService userService, IFlagService flagService)
    : ControllerBase<AdminFlagsController>(userService)
{
    protected override MenuItems ActiveMenuItem => MenuItems.AdminFlags;

    private async Task<IActionResult> RenderAsync(string viewPath, int labId, object model)
    {
        var labService = HttpContext.RequestServices.GetRequiredService<ILabService>();

        var lab = await labService.FindLabByIdAsync(labId, HttpContext.RequestAborted);
        if(lab == null)
            return RedirectToAction("RenderLabList", "AdminLabs");
        ViewData["Lab"] = lab;

        return await RenderViewAsync(viewPath, model);
    }

    [HttpGet]
    public async Task<IActionResult> RenderFlagListAsync(int labId)
    {
        var flags = await flagService.GetFlagListAsync(labId, HttpContext.RequestAborted);

        return await RenderAsync("~/Views/Admin/Flags/Index.cshtml", labId, flags);
    }

    private async Task<IActionResult> ShowEditFlagFormAsync(AdminFlagInputModel flagInput)
    {
        return await RenderAsync("~/Views/Admin/Flags/Edit.cshtml", flagInput.LabId, flagInput);
    }

    [HttpGet("edit")]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public async Task<IActionResult> ShowEditFlagFormAsync(int id, [FromServices] IMapper mapper)
    {
        var flag = await flagService.FindFlagByIdAsync(id, HttpContext.RequestAborted);
        if(flag == null)
        {
            PostStatusMessage = new StatusMessage(StatusMessageType.Error, Localizer["ShowEditFlagFormAsync:NotFound"]);
            return RedirectToAction("RenderLabList", "AdminLabs");
        }

        var flagInput = mapper.Map<AdminFlagInputModel>(flag);
        return await ShowEditFlagFormAsync(flagInput);
    }

    [HttpPost("edit")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public async Task<IActionResult> EditFlagAsync(AdminFlagInputModel flagInput, [FromServices] IMapper mapper)
    {
        // Check input
        if(!ModelState.IsValid || flagInput.Id == null)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["EditFlagAsync:InvalidInput"]);
            return await ShowEditFlagFormAsync(flagInput);
        }

        try
        {
            // Retrieve edited flag from database and apply changes
            var flag = await flagService.FindFlagByIdAsync(flagInput.Id.Value, HttpContext.RequestAborted);
            if(flag == null)
            {
                PostStatusMessage = new StatusMessage(StatusMessageType.Error, Localizer["EditFlagAsync:NotFound"]);
                return RedirectToAction("RenderLabList", "AdminLabs");
            }

            mapper.Map(flagInput, flag);

            await flagService.UpdateFlagAsync(flag, HttpContext.RequestAborted);

            PostStatusMessage = new StatusMessage(StatusMessageType.Success, Localizer["EditFlagAsync:Success"]) { AutoHide = true };
            return RedirectToAction("RenderFlagList", new { labId = flag.LabId });
        }
        catch(InvalidOperationException ex)
        {
            GetLogger().LogError(ex, "Edit flag");
            AddStatusMessage(StatusMessageType.Error, Localizer["EditFlagAsync:UnknownError"]);
            return await ShowEditFlagFormAsync(flagInput);
        }
    }

    private async Task<IActionResult> ShowCreateFlagFormAsync(AdminFlagInputModel flagInput)
    {
        return await RenderAsync("~/Views/Admin/Flags/Create.cshtml", flagInput.LabId, flagInput);
    }

    [HttpGet("create")]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public Task<IActionResult> ShowCreateFlagFormAsync(int labId)
        => ShowCreateFlagFormAsync(new AdminFlagInputModel { LabId = labId });

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public async Task<IActionResult> CreateFlagAsync(AdminFlagInputModel flagInput, string returnToForm, [FromServices] IMapper mapper)
    {
        // Check input
        if(!ModelState.IsValid)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateFlagAsync:InvalidInput"]);
            return await ShowCreateFlagFormAsync(flagInput);
        }

        try
        {
            // Create flag
            var flag = mapper.Map<Flag>(flagInput);
            await flagService.CreateFlagAsync(flag, HttpContext.RequestAborted);

            PostStatusMessage = new StatusMessage(StatusMessageType.Success, Localizer["CreateFlagAsync:Success"]) { AutoHide = true };

            if(!string.IsNullOrEmpty(returnToForm))
                return await ShowCreateFlagFormAsync(flagInput);
            return RedirectToAction("RenderFlagList", new { labId = flag.LabId });
        }
        catch(InvalidOperationException ex)
        {
            GetLogger().LogError(ex, "Create flag");
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateFlagAsync:UnknownError"]);
            return await ShowCreateFlagFormAsync(flagInput);
        }
    }

    [HttpPost("delete")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public async Task<IActionResult> DeleteFlagAsync(int id)
    {
        int labId = -1;
        try
        {
            // Input check
            var flag = await flagService.FindFlagByIdAsync(id, HttpContext.RequestAborted);
            if(flag == null)
            {
                PostStatusMessage = new StatusMessage(StatusMessageType.Error, Localizer["DeleteFlagAsync:NotFound"]);
                return RedirectToAction("RenderLabList", "AdminLabs");
            }

            labId = flag.LabId;

            // Delete flag
            await flagService.DeleteFlagAsync(id, HttpContext.RequestAborted);

            PostStatusMessage = new StatusMessage(StatusMessageType.Success, Localizer["DeleteFlagAsync:Success"]) { AutoHide = true };
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Delete flag");
            PostStatusMessage = new StatusMessage(StatusMessageType.Error, Localizer["DeleteFlagAsync:UnknownError"]);
        }

        if(labId == -1)
            return RedirectToAction("RenderLabList", "AdminLabs");
        return RedirectToAction("RenderFlagList", new { labId });
    }

    public static void RegisterMappings(Profile mappingProfile)
    {
        mappingProfile.CreateMap<Flag, AdminFlagInputModel>();
        mappingProfile.CreateMap<AdminFlagInputModel, Flag>();
    }
}
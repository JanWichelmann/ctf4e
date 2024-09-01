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

[Route("admin/exercises")]
[AnyUserPrivilege(UserPrivileges.ViewLabs)]
public class AdminExercisesController(IUserService userService, IExerciseService exerciseService)
    : ControllerBase<AdminExercisesController>(userService)
{
    protected override MenuItems ActiveMenuItem => MenuItems.AdminExercises;

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
    public async Task<IActionResult> RenderExerciseListAsync(int labId)
    {
        var exercises = await exerciseService.GetExerciseListAsync(labId, HttpContext.RequestAborted);

        return await RenderAsync("~/Views/Admin/Exercises/Index.cshtml", labId, exercises);
    }

    private async Task<IActionResult> ShowEditExerciseFormAsync(AdminExerciseInputModel exerciseInput)
    {
        return await RenderAsync("~/Views/Admin/Exercises/Edit.cshtml", exerciseInput.LabId, exerciseInput);
    }

    [HttpGet("edit")]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public async Task<IActionResult> ShowEditExerciseFormAsync(int id, [FromServices] IMapper mapper)
    {
        var exercise = await exerciseService.FindExerciseByIdAsync(id, HttpContext.RequestAborted);
        if(exercise == null)
        {
            PostStatusMessage = new StatusMessage(StatusMessageType.Error, Localizer["ShowEditExerciseFormAsync:NotFound"]);
            return RedirectToAction("RenderLabList", "AdminLabs");
        }

        var exerciseInput = mapper.Map<AdminExerciseInputModel>(exercise);
        return await ShowEditExerciseFormAsync(exerciseInput);
    }

    [HttpPost("edit")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public async Task<IActionResult> EditExerciseAsync(AdminExerciseInputModel exerciseInput, [FromServices] IMapper mapper)
    {
        // Check input
        if(!ModelState.IsValid || exerciseInput.Id == null)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["EditExerciseAsync:InvalidInput"]);
            return await ShowEditExerciseFormAsync(exerciseInput);
        }

        try
        {
            // Retrieve edited exercise from database and apply changes
            var exercise = await exerciseService.FindExerciseByIdAsync(exerciseInput.Id.Value, HttpContext.RequestAborted);
            if(exercise == null)
            {
                PostStatusMessage = new StatusMessage(StatusMessageType.Error, Localizer["EditExerciseAsync:NotFound"]);
                return RedirectToAction("RenderLabList", "AdminLabs");
            }

            mapper.Map(exerciseInput, exercise);

            await exerciseService.UpdateExerciseAsync(exercise, HttpContext.RequestAborted);

            PostStatusMessage = new StatusMessage(StatusMessageType.Success, Localizer["EditExerciseAsync:Success"]) { AutoHide = true };
            return RedirectToAction("RenderExerciseList", new { labId = exercise.LabId });
        }
        catch(InvalidOperationException ex)
        {
            GetLogger().LogError(ex, "Edit exercise");
            AddStatusMessage(StatusMessageType.Error, Localizer["EditExerciseAsync:UnknownError"]);
            return await ShowEditExerciseFormAsync(exerciseInput);
        }
    }

    private async Task<IActionResult> ShowCreateExerciseFormAsync(AdminExerciseInputModel exerciseInput)
    {
        return await RenderAsync("~/Views/Admin/Exercises/Create.cshtml", exerciseInput.LabId, exerciseInput);
    }

    [HttpGet("create")]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public Task<IActionResult> ShowCreateExerciseFormAsync(int labId)
        => ShowCreateExerciseFormAsync(new AdminExerciseInputModel { LabId = labId });

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public async Task<IActionResult> CreateExerciseAsync(AdminExerciseInputModel exerciseInput, string returnToForm, [FromServices] IMapper mapper)
    {
        // Check input
        if(!ModelState.IsValid)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateExerciseAsync:InvalidInput"]);
            return await ShowCreateExerciseFormAsync(exerciseInput);
        }

        try
        {
            // Create exercise
            var exercise = mapper.Map<Exercise>(exerciseInput);
            await exerciseService.CreateExerciseAsync(exercise, HttpContext.RequestAborted);

            PostStatusMessage = new StatusMessage(StatusMessageType.Success, Localizer["CreateExerciseAsync:Success"]) { AutoHide = true };

            if(!string.IsNullOrEmpty(returnToForm))
                return await ShowCreateExerciseFormAsync(exerciseInput);
            return RedirectToAction("RenderExerciseList", new { labId = exercise.LabId });
        }
        catch(InvalidOperationException ex)
        {
            GetLogger().LogError(ex, "Create exercise");
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateExerciseAsync:UnknownError"]);
            return await ShowCreateExerciseFormAsync(exerciseInput);
        }
    }

    [HttpPost("delete")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public async Task<IActionResult> DeleteExerciseAsync(int id)
    {
        int labId = -1;
        try
        {
            // Input check
            var exercise = await exerciseService.FindExerciseByIdAsync(id, HttpContext.RequestAborted);
            if(exercise == null)
            {
                PostStatusMessage = new StatusMessage(StatusMessageType.Error, Localizer["DeleteExerciseAsync:NotFound"]);
                return RedirectToAction("RenderLabList", "AdminLabs");
            }

            labId = exercise.LabId;

            // Delete exercise
            await exerciseService.DeleteExerciseAsync(id, HttpContext.RequestAborted);

            PostStatusMessage = new StatusMessage(StatusMessageType.Success, Localizer["DeleteExerciseAsync:Success"]) { AutoHide = true };
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Delete exercise");
            PostStatusMessage = new StatusMessage(StatusMessageType.Error, Localizer["DeleteExerciseAsync:UnknownError"]);
        }

        if(labId == -1)
            return RedirectToAction("RenderLabList", "AdminLabs");
        return RedirectToAction("RenderExerciseList", new { labId });
    }
    
    public static void RegisterMappings(Profile mappingProfile)
    {
        mappingProfile.CreateMap<Exercise, AdminExerciseInputModel>();
        mappingProfile.CreateMap<AdminExerciseInputModel, Exercise>();
    }
}
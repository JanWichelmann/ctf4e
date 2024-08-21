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

[Route("admin/exercises")]
[AnyUserPrivilege(UserPrivileges.ViewLabs)]
public class AdminExercisesController(IUserService userService, IExerciseService exerciseService)
    : ControllerBase<AdminExercisesController>(userService)
{
    protected override MenuItems ActiveMenuItem => MenuItems.AdminExercises;

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
    public async Task<IActionResult> RenderExerciseListAsync(int labId)
    {
        var exercises = await exerciseService.GetExercisesAsync(labId, HttpContext.RequestAborted);

        return await RenderAsync(ViewType.List, "~/Views/AdminExercises.cshtml", labId, exercises);
    }

    private async Task<IActionResult> ShowEditExerciseFormAsync(int? id, Exercise exercise = null)
    {
        // Retrieve by ID, if no object from a failed POST was passed
        if(id != null)
        {
            exercise = await exerciseService.FindExerciseByIdAsync(id.Value, HttpContext.RequestAborted);
            if(exercise == null)
                return RedirectToAction("RenderLabList", "AdminLabs");
        }

        if(exercise == null)
            return RedirectToAction("RenderLabList", "AdminLabs");

        return await RenderAsync(ViewType.Edit, "~/Views/AdminExercises.cshtml", exercise.LabId, exercise);
    }

    [HttpGet("edit")]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public Task<IActionResult> ShowEditExerciseFormAsync(int id)
    {
        return ShowEditExerciseFormAsync(id, null);
    }

    [HttpPost("edit")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public async Task<IActionResult> EditExerciseAsync(Exercise exerciseData)
    {
        // Check input
        if(!ModelState.IsValid)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["EditExerciseAsync:InvalidInput"]);
            return await ShowEditExerciseFormAsync(null, exerciseData);
        }

        try
        {
            // Retrieve edited exercise from database and apply changes
            var exercise = await exerciseService.FindExerciseByIdAsync(exerciseData.Id, HttpContext.RequestAborted);
            exercise.ExerciseNumber = exerciseData.ExerciseNumber;
            exercise.Name = exerciseData.Name;
            exercise.IsMandatory = exerciseData.IsMandatory;
            exercise.BasePoints = exerciseData.BasePoints;
            exercise.PenaltyPoints = exerciseData.PenaltyPoints;
            await exerciseService.UpdateExerciseAsync(exercise, HttpContext.RequestAborted);

            AddStatusMessage(StatusMessageType.Success, Localizer["EditExerciseAsync:Success"]);
            return await RenderExerciseListAsync(exercise.LabId);
        }
        catch(InvalidOperationException ex)
        {
            GetLogger().LogError(ex, "Edit exercise");
            AddStatusMessage(StatusMessageType.Error, Localizer["EditExerciseAsync:UnknownError"]);
            return await ShowEditExerciseFormAsync(null, exerciseData);
        }
    }

    [HttpGet("create")]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public async Task<IActionResult> ShowCreateExerciseFormAsync(int labId, Exercise exercise = null)
    {
        return await RenderAsync(ViewType.Create, "~/Views/AdminExercises.cshtml", labId, exercise);
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public async Task<IActionResult> CreateExerciseAsync(Exercise exerciseData, string returnToForm)
    {
        // Check input
        if(!ModelState.IsValid)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateExerciseAsync:InvalidInput"]);
            return await ShowCreateExerciseFormAsync(exerciseData.LabId, exerciseData);
        }

        try
        {
            // Create exercise
            var exercise = new Exercise
            {
                LabId = exerciseData.LabId,
                ExerciseNumber = exerciseData.ExerciseNumber,
                Name = exerciseData.Name,
                IsMandatory = exerciseData.IsMandatory,
                BasePoints = exerciseData.BasePoints,
                PenaltyPoints = exerciseData.PenaltyPoints
            };
            await exerciseService.CreateExerciseAsync(exercise, HttpContext.RequestAborted);

            AddStatusMessage(StatusMessageType.Success, Localizer["CreateExerciseAsync:Success"]);
                
            if(!string.IsNullOrEmpty(returnToForm))
                return await ShowCreateExerciseFormAsync(exerciseData.LabId, exerciseData);
            return await RenderExerciseListAsync(exerciseData.LabId);
        }
        catch(InvalidOperationException ex)
        {
            GetLogger().LogError(ex, "Create exercise");
            AddStatusMessage(StatusMessageType.Error, Localizer["CreateExerciseAsync:UnknownError"]);
            return await ShowCreateExerciseFormAsync(exerciseData.LabId, exerciseData);
        }
    }

    [HttpPost("delete")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public async Task<IActionResult> DeleteExerciseAsync(int id)
    {
        // Input check
        var exercise = await exerciseService.FindExerciseByIdAsync(id, HttpContext.RequestAborted);
        if(exercise == null)
            return RedirectToAction("RenderLabList", "AdminLabs");

        try
        {
            // Delete exercise
            await exerciseService.DeleteExerciseAsync(id, HttpContext.RequestAborted);

            AddStatusMessage(StatusMessageType.Success, Localizer["DeleteExerciseAsync:Success"]);
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Delete exercise");
            AddStatusMessage(StatusMessageType.Error, Localizer["DeleteExerciseAsync:UnknownError"]);
        }

        return await RenderExerciseListAsync(exercise.LabId);
    }

    public enum ViewType
    {
        List,
        Edit,
        Create
    }
}
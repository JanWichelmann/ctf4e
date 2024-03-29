﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Ctf4e.Server.Authorization;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Models;
using Ctf4e.Server.Services;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Ctf4e.Server.Controllers;

[Route("admin/exercises")]
[AnyUserPrivilege(UserPrivileges.ViewLabs)]
public class AdminExercisesController : ControllerBase
{
    private readonly IStringLocalizer<AdminExercisesController> _localizer;
    private readonly ILogger<AdminExercisesController> _logger;
    private readonly IExerciseService _exerciseService;
    private readonly ILabService _labService;

    public AdminExercisesController(IUserService userService, IStringLocalizer<AdminExercisesController> localizer, ILogger<AdminExercisesController> logger, IExerciseService exerciseService, ILabService labService)
        : base("~/Views/AdminExercises.cshtml", userService)
    {
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _exerciseService = exerciseService ?? throw new ArgumentNullException(nameof(exerciseService));
        _labService = labService ?? throw new ArgumentNullException(nameof(labService));
    }

    private async Task<IActionResult> RenderAsync(ViewType viewType, int labId, object model)
    {
        var lab = await _labService.GetLabAsync(labId, HttpContext.RequestAborted);
        if(lab == null)
            return RedirectToAction("RenderLabList", "AdminLabs");
        ViewData["Lab"] = lab;

        ViewData["ViewType"] = viewType;
        return await RenderViewAsync(MenuItems.AdminExercises, model);
    }

    [HttpGet]
    public async Task<IActionResult> RenderExerciseListAsync(int labId)
    {
        var exercises = await _exerciseService.GetExercisesAsync(labId).ToListAsync();

        return await RenderAsync(ViewType.List, labId, exercises);
    }

    private async Task<IActionResult> ShowEditExerciseFormAsync(int? id, Exercise exercise = null)
    {
        // Retrieve by ID, if no object from a failed POST was passed
        if(id != null)
        {
            exercise = await _exerciseService.GetExerciseAsync(id.Value, HttpContext.RequestAborted);
            if(exercise == null)
                return RedirectToAction("RenderLabList", "AdminLabs");
        }

        if(exercise == null)
            return RedirectToAction("RenderLabList", "AdminLabs");

        return await RenderAsync(ViewType.Edit, exercise.LabId, exercise);
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
            AddStatusMessage(_localizer["EditExerciseAsync:InvalidInput"], StatusMessageTypes.Error);
            return await ShowEditExerciseFormAsync(null, exerciseData);
        }

        try
        {
            // Retrieve edited exercise from database and apply changes
            var exercise = await _exerciseService.GetExerciseAsync(exerciseData.Id, HttpContext.RequestAborted);
            exercise.ExerciseNumber = exerciseData.ExerciseNumber;
            exercise.Name = exerciseData.Name;
            exercise.IsMandatory = exerciseData.IsMandatory;
            exercise.BasePoints = exerciseData.BasePoints;
            exercise.PenaltyPoints = exerciseData.PenaltyPoints;
            await _exerciseService.UpdateExerciseAsync(exercise, HttpContext.RequestAborted);

            AddStatusMessage(_localizer["EditExerciseAsync:Success"], StatusMessageTypes.Success);
            return await RenderExerciseListAsync(exercise.LabId);
        }
        catch(InvalidOperationException ex)
        {
            _logger.LogError(ex, "Edit exercise");
            AddStatusMessage(_localizer["EditExerciseAsync:UnknownError"], StatusMessageTypes.Error);
            return await ShowEditExerciseFormAsync(null, exerciseData);
        }
    }

    [HttpGet("create")]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public async Task<IActionResult> ShowCreateExerciseFormAsync(int labId, Exercise exercise = null)
    {
        return await RenderAsync(ViewType.Create, labId, exercise);
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public async Task<IActionResult> CreateExerciseAsync(Exercise exerciseData, string returnToForm)
    {
        // Check input
        if(!ModelState.IsValid)
        {
            AddStatusMessage(_localizer["CreateExerciseAsync:InvalidInput"], StatusMessageTypes.Error);
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
            await _exerciseService.CreateExerciseAsync(exercise, HttpContext.RequestAborted);

            AddStatusMessage(_localizer["CreateExerciseAsync:Success"], StatusMessageTypes.Success);
                
            if(!string.IsNullOrEmpty(returnToForm))
                return await ShowCreateExerciseFormAsync(exerciseData.LabId, exerciseData);
            return await RenderExerciseListAsync(exerciseData.LabId);
        }
        catch(InvalidOperationException ex)
        {
            _logger.LogError(ex, "Create exercise");
            AddStatusMessage(_localizer["CreateExerciseAsync:UnknownError"], StatusMessageTypes.Error);
            return await ShowCreateExerciseFormAsync(exerciseData.LabId, exerciseData);
        }
    }

    [HttpPost("delete")]
    [ValidateAntiForgeryToken]
    [AnyUserPrivilege(UserPrivileges.EditLabs)]
    public async Task<IActionResult> DeleteExerciseAsync(int id)
    {
        // Input check
        var exercise = await _exerciseService.GetExerciseAsync(id, HttpContext.RequestAborted);
        if(exercise == null)
            return RedirectToAction("RenderLabList", "AdminLabs");

        try
        {
            // Delete exercise
            await _exerciseService.DeleteExerciseAsync(id, HttpContext.RequestAborted);

            AddStatusMessage(_localizer["DeleteExerciseAsync:Success"], StatusMessageTypes.Success);
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Delete exercise");
            AddStatusMessage(_localizer["DeleteExerciseAsync:UnknownError"], StatusMessageTypes.Error);
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
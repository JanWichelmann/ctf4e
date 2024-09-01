using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ctf4e.LabServer.Configuration;
using Ctf4e.LabServer.Constants;
using Ctf4e.LabServer.InputModels;
using Ctf4e.LabServer.Options;
using Ctf4e.LabServer.Services;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Ctf4e.LabServer.Controllers;

[Route("admin/config")]
[Authorize(Policy = AuthenticationStrings.PolicyAdminMode)]
public class AdminConfigurationController(IOptions<LabOptions> labOptions, ILabConfigurationService labConfiguration, IStateService stateService)
    : ControllerBase<AdminConfigurationController>
{
    protected override MenuItems ActiveMenuItem => MenuItems.AdminConfiguration;

    private IActionResult ShowForm(AdminConfigurationInputModel configurationInput)
    {
        return RenderView("~/Views/Admin/Configuration/Index.cshtml", configurationInput);
    }

    [HttpGet]
    public async Task<IActionResult> ShowFormAsync()
    {
        try
        {
            // Open configuration file
            var configFile = new FileInfo(labOptions.Value.LabConfigurationFile);
            await using var configFileStream = configFile.Open(FileMode.Open);

            // Check whether configuration file is writable by this application
            bool writable = !configFile.IsReadOnly && configFileStream.CanWrite;
            ViewData["Writable"] = writable;
            if(!writable)
                AddStatusMessage(StatusMessageType.Warning, Localizer["RenderAsync:ConfigNonWritable"]);

            // Read configuration
            using var configFileReader = new StreamReader(configFileStream);
            var configurationInput = new AdminConfigurationInputModel
            {
                Configuration = await configFileReader.ReadToEndAsync()
            };

            return ShowForm(configurationInput);
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Read configuration file");
            AddStatusMessage(StatusMessageType.Error, Localizer["RenderAsync:UnknownError"]);
            return RenderView("~/Views/Admin/Configuration/Empty.cshtml");
        }
    }

    [HttpPost("update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateConfigurationAsync(AdminConfigurationInputModel configurationInput)
    {
        if(!ModelState.IsValid)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["UpdateConfigurationAsync:InvalidInput"]);
            return ShowForm(configurationInput);
        }

        // Parse configuration and do some sanity checks
        bool error = false;
        try
        {
            var newOptions = JsonConvert.DeserializeObject<LabConfiguration>(configurationInput.Configuration);

            HashSet<int> exerciseIds = new HashSet<int>();
            foreach(var exercise in newOptions.Exercises)
            {
                // Make sure that exercise IDs are unique
                if(exerciseIds.Contains(exercise.Id))
                {
                    AddStatusMessage(StatusMessageType.Error, Localizer["UpdateConfigurationAsync:DuplicateExerciseId", exercise.Id]);
                    error = true;
                }

                exerciseIds.Add(exercise.Id);

                // Run exercise self-check
                if(!exercise.Validate(out string errorMessage))
                {
                    AddStatusMessage(StatusMessageType.Error, Localizer["UpdateConfigurationAsync:ValidationError", exercise.Id, errorMessage]);
                    error = true;
                }
            }
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Parse new configuration");
            AddStatusMessage(StatusMessageType.Error, Localizer["UpdateConfigurationAsync:ErrorParsingNewConfig"]);
            error = true;
        }

        if(error)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["UpdateConfigurationAsync:Error"]);
            return ShowForm(configurationInput);
        }

        try
        {
            // Update configuration
            await System.IO.File.WriteAllTextAsync(labOptions.Value.LabConfigurationFile, configurationInput.Configuration, CancellationToken.None);

            // Reload
            await labConfiguration.ReadConfigurationAsync(HttpContext.RequestAborted);
            await stateService.ReloadAsync(HttpContext.RequestAborted);

            PostStatusMessage = new StatusMessage(StatusMessageType.Success, Localizer["UpdateConfigurationAsync:Success"]);
            return RedirectToAction("ShowForm");
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Update configuration");
            AddStatusMessage(StatusMessageType.Error, Localizer["UpdateConfigurationAsync:UnknownError"]);
            return ShowForm(configurationInput);
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Ctf4e.LabServer.Configuration;
using Ctf4e.LabServer.Constants;
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
public class AdminConfigurationController(IOptionsSnapshot<LabOptions> labOptions, ILabConfigurationService labConfiguration, IStateService stateService)
    : ControllerBase<AdminConfigurationController>
{
    protected override MenuItems ActiveMenuItem => MenuItems.AdminConfiguration;

    private async Task<IActionResult> RenderAsync(string configuration = null)
    {
        try
        {
            // Open configuration file
            var configFile = new FileInfo(labOptions.Value.LabConfigurationFile);
            await using var configFileStream = configFile.Open(FileMode.Open);

            // Check whether configuration file is writable by this application
            bool writable = configFileStream.CanWrite;
            ViewData["Writable"] = writable;
            if(!writable)
                AddStatusMessage(StatusMessageType.Warning, Localizer["RenderAsync:ConfigNonWritable"]);

            // Read configuration
            if(configuration == null)
            {
                using var configFileReader = new StreamReader(configFileStream);
                ViewData["Configuration"] = await configFileReader.ReadToEndAsync();
            }
            else
                ViewData["Configuration"] = configuration;
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Read configuration file");
            AddStatusMessage(StatusMessageType.Error, Localizer["RenderAsync:UnknownError"]);
        }

        return RenderView("~/Views/AdminConfiguration.cshtml");
    }

    [HttpGet("")]
    public Task<IActionResult> RenderPageAsync()
    {
        return RenderAsync();
    }

    [HttpPost("update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateConfigurationAsync(string configuration)
    {
        // Parse configuration and do some sanity checks
        bool error = false;
        try
        {
            var newOptions = JsonConvert.DeserializeObject<LabConfiguration>(configuration);

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
                    AddStatusMessage(StatusMessageType.Error, Localizer["UpdateConfigurationAsync:ValidationError", errorMessage]);
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
            return await RenderAsync(configuration);
        }

        try
        {
            // Update configuration
            await System.IO.File.WriteAllTextAsync(labOptions.Value.LabConfigurationFile, configuration);
                
            // Reload
            await labConfiguration.ReadConfigurationAsync();
            stateService.Reload();
                
            AddStatusMessage(StatusMessageType.Success, Localizer["UpdateConfigurationAsync:Success"]);
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Update configuration");
            AddStatusMessage(StatusMessageType.Error, Localizer["UpdateConfigurationAsync:UnknownError"]);
        }

        return await RenderAsync();
    }
}
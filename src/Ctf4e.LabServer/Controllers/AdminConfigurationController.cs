using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
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
    public async Task<IActionResult> ShowFormAsync(string configurationFile)
    {
        try
        {
            configurationFile ??= labOptions.Value.LabConfigurationFile;
            if(configurationFile == null || !labConfiguration.IsConfigurationFile(configurationFile))
            {
                AddStatusMessage(StatusMessageType.Error, Localizer["ShowFormAsync:ConfigFileNotFound"]);
                return RenderView("~/Views/Admin/Configuration/Empty.cshtml");
            }

            // Open configuration file
            var configFile = new FileInfo(configurationFile);
            await using var configFileStream = configFile.Open(FileMode.Open);

            // Check whether configuration file is writable by this application
            bool writable = !configFile.IsReadOnly && configFileStream.CanWrite;
            if(!writable)
                AddStatusMessage(StatusMessageType.Warning, Localizer["RenderAsync:ConfigNonWritable"]);

            // Read configuration
            using var configFileReader = new StreamReader(configFileStream);
            var configurationInput = new AdminConfigurationInputModel
            {
                FileToChange = configurationFile,
                Configuration = await configFileReader.ReadToEndAsync(),
                Writable = writable
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

        // Parse and check configuration
        LabConfiguration parsedNewConfiguration;
        List<string> issues = [];
        try
        {
            if(!labConfiguration.IsConfigurationFile(configurationInput.FileToChange))
            {
                AddStatusMessage(StatusMessageType.Error, Localizer["UpdateConfigurationAsync:ConfigFileNotFound"]);
                return ShowForm(configurationInput);
            }

            // Deserialize; this will implicitly check whether this configuration part is syntactically correct
            parsedNewConfiguration = JsonSerializer.Deserialize<LabConfiguration>(configurationInput.Configuration);

            // Check whether the full configuration stays valid with the update
            await labConfiguration.DeserializeAndCheckConfigurationAsync(
                configurationInput.FileToChange,
                configurationInput.Configuration,
                issues,
                HttpContext.RequestAborted
            );

            if(issues.Count > 0)
            {
                AddStatusMessage(StatusMessageType.Error, Localizer["UpdateConfigurationAsync:InvalidConfig"]);
                AddStatusMessage(StatusMessageType.Error, string.Join("\n", issues));

                return ShowForm(configurationInput);
            }
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Parse new configuration");
            AddStatusMessage(StatusMessageType.Error, Localizer["UpdateConfigurationAsync:ErrorParsingNewConfig"]);
            return ShowForm(configurationInput);
        }

        // Write configuration and reload state
        try
        {
            GetLogger().LogInformation("Updating configuration file {File}", configurationInput.FileToChange);
            await labConfiguration.WriteConfigurationAsync(configurationInput.FileToChange, parsedNewConfiguration, CancellationToken.None);

            await stateService.ReloadAsync(CancellationToken.None);

            PostStatusMessage = new StatusMessage(StatusMessageType.Success, Localizer["UpdateConfigurationAsync:Success"]);
            return RedirectToAction("ShowForm", new { configurationFile = configurationInput.FileToChange });
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "Update configuration");
            AddStatusMessage(StatusMessageType.Error, Localizer["UpdateConfigurationAsync:UnknownError"]);
            return ShowForm(configurationInput);
        }
    }
}
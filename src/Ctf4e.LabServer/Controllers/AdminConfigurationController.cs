using System;
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
        LabConfiguration newConfiguration;
        try
        {
            if(!labConfiguration.DeserializeAndCheckConfiguration(configurationInput.Configuration, out var issues, out newConfiguration))
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
            await labConfiguration.WriteConfigurationAsync(newConfiguration, CancellationToken.None);
            
            await stateService.ReloadAsync(CancellationToken.None);

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
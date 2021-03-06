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
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Ctf4e.LabServer.Controllers
{
    [Route("admin/config")]
    [Authorize(Policy = AuthenticationStrings.PolicyAdminMode)]
    public class AdminConfigurationController : ControllerBase
    {
        private readonly IOptionsSnapshot<LabOptions> _labOptions;
        private readonly ILabConfigurationService _labConfiguration;
        private readonly IStateService _stateService;

        public AdminConfigurationController(IOptionsSnapshot<LabOptions> labOptions, ILabConfigurationService labConfiguration, IStateService stateService)
            : base("~/Views/AdminConfiguration.cshtml", labOptions, labConfiguration)
        {
            _labOptions = labOptions;
            _labConfiguration = labConfiguration ?? throw new ArgumentNullException(nameof(labConfiguration));
            _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
        }

        private async Task<IActionResult> RenderAsync(string configuration = null)
        {
            try
            {
                // Open configuration file
                var configFile = new FileInfo(_labOptions.Value.LabConfigurationFile);
                await using var configFileStream = configFile.Open(FileMode.Open);

                // Check whether configuration file is writable by this application
                bool writable = configFileStream.CanWrite;
                ViewData["Writable"] = writable;
                if(!writable)
                    AddStatusMessage("Für die Konfigurationsdatei sind keine Schreibrechte gesetzt. Diese kann daher nur gelesen, aber nicht verändert werden.", StatusMessageTypes.Warning);

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
                AddStatusMessage("Die Konfigurationsdatei konnte nicht gelesen werden: " + ex.Message, StatusMessageTypes.Error);
                return RenderView(MenuItems.AdminConfiguration);
            }

            return RenderView(MenuItems.AdminConfiguration);
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
                        AddStatusMessage("Doppelte Aufgabe-ID: " + exercise.Id, StatusMessageTypes.Error);
                        error = true;
                    }

                    exerciseIds.Add(exercise.Id);

                    // Run exercise self-check
                    if(!exercise.Validate(out string errorMessage))
                    {
                        AddStatusMessage(errorMessage, StatusMessageTypes.Error);
                        error = true;
                    }
                }
            }
            catch(Exception)
            {
                AddStatusMessage("Konnte neue Konfiguration nicht parsen. Ist das JSON gültig?", StatusMessageTypes.Error);
                error = true;
            }

            if(error)
            {
                AddStatusMessage("Es sind Fehler aufgetreten. Die neue Konfiguration wurde nicht gespeichert.", StatusMessageTypes.Error);
                return await RenderAsync(configuration);
            }

            try
            {
                // Update configuration
                await System.IO.File.WriteAllTextAsync(_labOptions.Value.LabConfigurationFile, configuration);
                
                // Reload
                await _labConfiguration.ReadConfigurationAsync();
                _stateService.Reload();
                
                AddStatusMessage("Aktualisieren der Konfiguration erfolgreich.", StatusMessageTypes.Success);
            }
            catch(Exception ex)
            {
                AddStatusMessage("Konnte neue Konfiguration nicht anwenden: " + ex.Message + "\nMöglicherweise ist das System jetzt in einem inkonsistenten Zustand. Es wird empfohlen, die aktuelle Konfiguration zu prüfen und einen Neustart durchzuführen.", StatusMessageTypes.Error);
            }

            return await RenderAsync();
        }
    }
}
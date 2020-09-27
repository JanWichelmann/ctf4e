using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ctf4e.Api.Options;
using Ctf4e.LabServer.Constants;
using Ctf4e.LabServer.Options;
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
        public AdminConfigurationController(IOptionsSnapshot<LabOptions> labOptions)
            : base("~/Views/AdminConfiguration.cshtml", labOptions)
        {
        }

        private async Task<IActionResult> RenderAsync(string configuration = null)
        {
            try
            {
                // Open configuration file
                var configFile = new FileInfo(Environment.GetEnvironmentVariable("CTF4E_LAB_CONFIG_FILE"));
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
                var newOptions = JsonConvert.DeserializeObject<ConfigFile>(configuration).LabOptions;

                // Group state directory should exist
                if(!Directory.Exists(newOptions.GroupStateDirectory))
                {
                    AddStatusMessage("Das angegebene Verzeichnis mit Gruppendaten existiert nicht.", StatusMessageTypes.Error);
                    error = true;
                }

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

                    // Exercises should either point to a CTF exercise or a flag code
                    if(exercise.CtfExerciseNumber == null && exercise.FlagCode == null)
                    {
                        AddStatusMessage($"Für die Aufgabe \"{exercise.Title}\" (#{exercise.Id}) sind weder CTF-Aufgabennummer noch Flag-Code gesetzt.", StatusMessageTypes.Error);
                        error = true;
                    }

                    // There must be at least one solution
                    if(!exercise.ValidSolutions.Any())
                    {
                        AddStatusMessage($"Für die Aufgabe \"{exercise.Title}\" (#{exercise.Id}) existieren keine Lösungen.", StatusMessageTypes.Error);
                        error = true;
                    }

                    // Make sure that solutions have names, if a specific one must be found
                    HashSet<string> solutionNames = new HashSet<string>();
                    foreach(var solution in exercise.ValidSolutions)
                    {
                        if(!exercise.AllowAnySolution)
                        {
                            if(solution.Name == null)
                            {
                                AddStatusMessage($"Für die Lösung \"{solution.Value}\" in Aufgabe \"{exercise.Title}\" (#{exercise.Id}) wurde kein Name festgelegt.", StatusMessageTypes.Error);
                                error = true;
                            }
                            else if(solutionNames.Contains(solution.Name))
                            {
                                AddStatusMessage($"Der Name der Lösung \"{solution.Value}\" in Aufgabe \"{exercise.Title}\" (#{exercise.Id}) ist nicht eindeutig.", StatusMessageTypes.Error);
                                error = true;
                            }
                            else
                                solutionNames.Add(solution.Name);
                        }
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
                await System.IO.File.WriteAllTextAsync(Environment.GetEnvironmentVariable("CTF4E_LAB_CONFIG_FILE"), configuration);
                AddStatusMessage("Aktualisieren der Konfiguration erfolgreich.", StatusMessageTypes.Success);
            }
            catch(Exception ex)
            {
                AddStatusMessage("Konnte neue Konfiguration nicht schreiben:" + ex.Message, StatusMessageTypes.Error);
            }

            return await RenderAsync();
        }

        /// <summary>
        /// Utility class for test-deserialization of changed configuration data.
        /// </summary>
        private class ConfigFile
        {
            public CtfApiOptions CtfApiOptions { get; set; }
            public LabOptions LabOptions { get; set; }
        }
    }
}
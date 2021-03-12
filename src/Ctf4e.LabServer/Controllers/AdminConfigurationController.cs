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
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
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
        private readonly IStringLocalizer<AdminConfigurationController> _localizer;
        private readonly ILogger<AdminConfigurationController> _logger;
        private readonly IStateService _stateService;

        public AdminConfigurationController(IOptionsSnapshot<LabOptions> labOptions, ILabConfigurationService labConfiguration, IStringLocalizer<AdminConfigurationController> localizer, ILogger<AdminConfigurationController> logger, IStateService stateService)
            : base("~/Views/AdminConfiguration.cshtml", labOptions, labConfiguration)
        {
            _labOptions = labOptions;
            _labConfiguration = labConfiguration ?? throw new ArgumentNullException(nameof(labConfiguration));
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                    AddStatusMessage(_localizer["RenderAsync:ConfigNonWritable"], StatusMessageTypes.Warning);

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
                _logger.LogError(ex, "Read configuration file");
                AddStatusMessage(_localizer["RenderAsync:UnknownError"], StatusMessageTypes.Error);
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
                        AddStatusMessage(_localizer["UpdateConfigurationAsync:DuplicateExerciseId", exercise.Id], StatusMessageTypes.Error);
                        error = true;
                    }

                    exerciseIds.Add(exercise.Id);

                    // Run exercise self-check
                    if(!exercise.Validate(out string errorMessage))
                    {
                        AddStatusMessage(_localizer["UpdateConfigurationAsync:ValidationError", errorMessage], StatusMessageTypes.Error);
                        error = true;
                    }
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Parse new configuration");
                AddStatusMessage(_localizer["UpdateConfigurationAsync:ErrorParsingNewConfig"], StatusMessageTypes.Error);
                error = true;
            }

            if(error)
            {
                AddStatusMessage(_localizer["UpdateConfigurationAsync:Error"], StatusMessageTypes.Error);
                return await RenderAsync(configuration);
            }

            try
            {
                // Update configuration
                await System.IO.File.WriteAllTextAsync(_labOptions.Value.LabConfigurationFile, configuration);
                
                // Reload
                await _labConfiguration.ReadConfigurationAsync();
                _stateService.Reload();
                
                AddStatusMessage(_localizer["UpdateConfigurationAsync:Success"], StatusMessageTypes.Success);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Update configuration");
                AddStatusMessage(_localizer["UpdateConfigurationAsync:UnknownError"], StatusMessageTypes.Error);
            }

            return await RenderAsync();
        }
    }
}
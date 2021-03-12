using System;
using System.Linq;
using System.Threading.Tasks;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Services;
using Ctf4e.Server.ViewModels;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Ctf4e.Server.Controllers
{
    [Route("admin/config")]
    [Authorize(Policy = AuthenticationStrings.PolicyIsAdmin)]
    public class AdminConfigurationController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IStringLocalizer<AdminConfigurationController> _localizer;
        private readonly ILogger<AdminConfigurationController> _logger;
        private readonly IConfigurationService _configurationService;

        public AdminConfigurationController(IUserService userService, IStringLocalizer<AdminConfigurationController> localizer, ILogger<AdminConfigurationController> logger, IConfigurationService configurationService)
            : base("~/Views/AdminConfiguration.cshtml", userService)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        }

        private async Task<IActionResult> RenderAsync(AdminConfigurationData configurationData)
        {
            var config = configurationData ?? new AdminConfigurationData
            {
                FlagHalfPointsSubmissionCount = await _configurationService.GetFlagHalfPointsSubmissionCountAsync(HttpContext.RequestAborted),
                FlagMinimumPointsDivisor = await _configurationService.GetFlagMinimumPointsDivisorAsync(HttpContext.RequestAborted),
                ScoreboardEntryCount = await _configurationService.GetScoreboardEntryCountAsync(HttpContext.RequestAborted),
                ScoreboardCachedSeconds = await _configurationService.GetScoreboardCachedSecondsAsync(HttpContext.RequestAborted),
                PassAsGroup = await _configurationService.GetPassAsGroupAsync(HttpContext.RequestAborted),
                PageTitle = await _configurationService.GetPageTitleAsync(HttpContext.RequestAborted),
                NavbarTitle = await _configurationService.GetNavbarTitleAsync(HttpContext.RequestAborted),
                GroupSizeMin = await _configurationService.GetGroupSizeMinAsync(HttpContext.RequestAborted),
                GroupSizeMax = await _configurationService.GetGroupSizeMaxAsync(HttpContext.RequestAborted),
                GroupSelectionPageText = await _configurationService.GetGroupSelectionPageTextAsync(HttpContext.RequestAborted)
            };

            int groupCount = await _userService.GetGroupsAsync().CountAsync(HttpContext.RequestAborted);
            ViewData["GroupCount"] = groupCount;

            return await RenderViewAsync(MenuItems.AdminConfiguration, config);
        }

        [HttpGet]
        public Task<IActionResult> RenderAsync()
        {
            return RenderAsync(null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateConfigAsync(AdminConfigurationData configurationData)
        {
            if(!ModelState.IsValid)
            {
                AddStatusMessage(_localizer["UpdateConfigAsync:InvalidInput"], StatusMessageTypes.Error);
                return await RenderAsync(configurationData);
            }

            // Update configuration
            try
            {
                if(configurationData.FlagHalfPointsSubmissionCount <= 1)
                    configurationData.FlagHalfPointsSubmissionCount = 2;
                await _configurationService.SetFlagHalfPointsSubmissionCountAsync(configurationData.FlagHalfPointsSubmissionCount, HttpContext.RequestAborted);
                
                if(configurationData.FlagMinimumPointsDivisor <= 1)
                    configurationData.FlagMinimumPointsDivisor = 2;
                await _configurationService.SetFlagMinimumPointsDivisorAsync(configurationData.FlagMinimumPointsDivisor, HttpContext.RequestAborted);
                
                if(configurationData.ScoreboardEntryCount < 3)
                    configurationData.ScoreboardEntryCount = 3;
                await _configurationService.SetScoreboardEntryCountAsync(configurationData.ScoreboardEntryCount, HttpContext.RequestAborted);
                
                if(configurationData.ScoreboardCachedSeconds < 0)
                    configurationData.ScoreboardCachedSeconds = 0;
                await _configurationService.SetScoreboardCachedSecondsAsync(configurationData.ScoreboardCachedSeconds, HttpContext.RequestAborted);
                
                await _configurationService.SetPassAsGroupAsync(configurationData.PassAsGroup, HttpContext.RequestAborted);
                
                await _configurationService.SetPageTitleAsync(configurationData.PageTitle, HttpContext.RequestAborted);
                await _configurationService.SetNavbarTitleAsync(configurationData.NavbarTitle, HttpContext.RequestAborted);

                if(configurationData.GroupSizeMin <= 0)
                    configurationData.GroupSizeMin = 1;
                if(configurationData.GroupSizeMin > configurationData.GroupSizeMax)
                    configurationData.GroupSizeMax = configurationData.GroupSizeMin + 1;
                await _configurationService.SetGroupSizeMinAsync(configurationData.GroupSizeMin, HttpContext.RequestAborted);
                await _configurationService.SetGroupSizeMaxAsync(configurationData.GroupSizeMax, HttpContext.RequestAborted);

                await _configurationService.SetGroupSelectionPageTextAsync(configurationData.GroupSelectionPageText, HttpContext.RequestAborted);

                AddStatusMessage(_localizer["UpdateConfigAsync:Success"], StatusMessageTypes.Success);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Update configuration");
                AddStatusMessage(_localizer["UpdateConfigAsync:UnknownError"], StatusMessageTypes.Error);
                return await RenderAsync(configurationData);
            }

            return await RenderAsync(null);
        }
    }
}
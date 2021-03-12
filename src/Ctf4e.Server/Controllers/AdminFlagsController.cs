using System;
using System.Linq;
using System.Threading.Tasks;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Models;
using Ctf4e.Server.Options;
using Ctf4e.Server.Services;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ctf4e.Server.Controllers
{
    [Route("admin/flags")]
    [Authorize(Policy = AuthenticationStrings.PolicyIsAdmin)]
    public class AdminFlagsController : ControllerBase
    {
        private readonly IStringLocalizer<AdminFlagsController> _localizer;
        private readonly ILogger<AdminFlagsController> _logger;
        private readonly IFlagService _flagService;
        private readonly ILabService _labService;

        public AdminFlagsController(IUserService userService, IOptions<MainOptions> mainOptions, IStringLocalizer<AdminFlagsController> localizer, ILogger<AdminFlagsController> logger, IFlagService flagService, ILabService labService)
            : base("~/Views/AdminFlags.cshtml", userService, mainOptions)
        {
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _flagService = flagService ?? throw new ArgumentNullException(nameof(flagService));
            _labService = labService ?? throw new ArgumentNullException(nameof(labService));
        }

        private async Task<IActionResult> RenderAsync(ViewType viewType, int labId, object model)
        {
            var lab = await _labService.GetLabAsync(labId, HttpContext.RequestAborted);
            if(lab == null)
                return RedirectToAction("RenderLabList", "AdminLabs");
            ViewData["Lab"] = await _labService.GetLabAsync(labId, HttpContext.RequestAborted);

            ViewData["ViewType"] = viewType;
            return await RenderViewAsync(MenuItems.AdminFlags, model);
        }

        [HttpGet]
        public async Task<IActionResult> RenderFlagListAsync(int labId)
        {
            var flags = await _flagService.GetFlagsAsync(labId).ToListAsync();

            return await RenderAsync(ViewType.List, labId, flags);
        }

        private async Task<IActionResult> ShowEditFlagFormAsync(int? id, Flag flag = null)
        {
            // Retrieve by ID, if no object from a failed POST was passed
            if(id != null)
            {
                flag = await _flagService.GetFlagAsync(id.Value, HttpContext.RequestAborted);
                if(flag == null)
                    return RedirectToAction("RenderLabList", "AdminLabs");
            }

            if(flag == null)
                return RedirectToAction("RenderLabList", "AdminLabs");

            return await RenderAsync(ViewType.Edit, flag.LabId, flag);
        }

        [HttpGet("edit")]
        public Task<IActionResult> ShowEditFlagFormAsync(int id)
        {
            return ShowEditFlagFormAsync(id, null);
        }

        [HttpPost("edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditFlagAsync(Flag flagData)
        {
            // Check input
            if(!ModelState.IsValid)
            {
                AddStatusMessage(_localizer["EditFlagAsync:InvalidInput"], StatusMessageTypes.Error);
                return await ShowEditFlagFormAsync(null, flagData);
            }

            try
            {
                // Retrieve edited flag from database and apply changes
                var flag = await _flagService.GetFlagAsync(flagData.Id, HttpContext.RequestAborted);
                flag.Code = flagData.Code;
                flag.Description = flagData.Description;
                flag.BasePoints = flagData.BasePoints;
                flag.IsBounty = flagData.IsBounty;
                await _flagService.UpdateFlagAsync(flag, HttpContext.RequestAborted);

                AddStatusMessage(_localizer["EditFlagAsync:Success"], StatusMessageTypes.Success);
                return await RenderFlagListAsync(flag.LabId);
            }
            catch(InvalidOperationException ex)
            {
                _logger.LogError(ex, "Edit flag");
                AddStatusMessage(_localizer["EditFlagAsync:UnknownError"], StatusMessageTypes.Error);
                return await ShowEditFlagFormAsync(null, flagData);
            }
        }

        [HttpGet("create")]
        public async Task<IActionResult> ShowCreateFlagFormAsync(int labId, Flag flag = null)
        {
            return await RenderAsync(ViewType.Create, labId, flag);
        }

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFlagAsync(Flag flagData)
        {
            // Check input
            if(!ModelState.IsValid)
            {
                AddStatusMessage(_localizer["CreateFlagAsync:InvalidInput"], StatusMessageTypes.Error);
                return await ShowCreateFlagFormAsync(flagData.LabId, flagData);
            }

            try
            {
                // Create flag
                var flag = new Flag
                {
                    Code = flagData.Code,
                    Description = flagData.Description,
                    BasePoints = flagData.BasePoints,
                    IsBounty = flagData.IsBounty,
                    LabId = flagData.LabId
                };
                await _flagService.CreateFlagAsync(flag, HttpContext.RequestAborted);

                AddStatusMessage(_localizer["CreateFlagAsync:Success"], StatusMessageTypes.Success);
                return await RenderFlagListAsync(flagData.LabId);
            }
            catch(InvalidOperationException ex)
            {
                _logger.LogError(ex, "Create flag");
                AddStatusMessage(_localizer["CreateFlagAsync:UnknownError"], StatusMessageTypes.Error);
                return await ShowCreateFlagFormAsync(flagData.LabId, flagData);
            }
        }

        [HttpPost("delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFlagAsync(int id)
        {
            // Input check
            var flag = await _flagService.GetFlagAsync(id, HttpContext.RequestAborted);
            if(flag == null)
                return RedirectToAction("RenderLabList", "AdminLabs");

            try
            {
                // Delete flag
                await _flagService.DeleteFlagAsync(id, HttpContext.RequestAborted);

                AddStatusMessage(_localizer["DeleteFlagAsync:Success"], StatusMessageTypes.Success);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Delete flag");
                AddStatusMessage(_localizer["DeleteFlagAsync:UnknownError"], StatusMessageTypes.Error);
            }

            return await RenderFlagListAsync(flag.LabId);
        }

        public enum ViewType
        {
            List,
            Edit,
            Create
        }
    }
}
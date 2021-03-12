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
    [Route("admin/labs")]
    [Authorize(Policy = AuthenticationStrings.PolicyIsAdmin)]
    public class AdminLabsController : ControllerBase
    {
        private readonly IStringLocalizer<AdminLabsController> _localizer;
        private readonly ILogger<AdminLabsController> _logger;
        private readonly ILabService _labService;

        public AdminLabsController(IUserService userService, IOptions<MainOptions> mainOptions, IStringLocalizer<AdminLabsController> localizer, ILogger<AdminLabsController> logger, ILabService labService)
            : base("~/Views/AdminLabs.cshtml", userService, mainOptions)
        {
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _labService = labService ?? throw new ArgumentNullException(nameof(labService));
        }

        private Task<IActionResult> RenderAsync(ViewType viewType, object model)
        {
            ViewData["ViewType"] = viewType;

            return RenderViewAsync(MenuItems.AdminLabs, model);
        }

        [HttpGet]
        public async Task<IActionResult> RenderLabListAsync()
        {
            // Pass labs
            var labs = await _labService.GetFullLabsAsync().ToListAsync();

            return await RenderAsync(ViewType.List, labs);
        }

        private async Task<IActionResult> ShowEditLabFormAsync(int? id, Lab lab = null)
        {
            // Retrieve by ID, if no object from a failed POST was passed
            if(id != null)
            {
                lab = await _labService.GetLabAsync(id.Value, HttpContext.RequestAborted);
                if(lab == null)
                {
                    AddStatusMessage(_localizer["ShowEditLabFormAsync:NotFound"], StatusMessageTypes.Error);
                    return await RenderLabListAsync();
                }
            }

            if(lab == null)
            {
                AddStatusMessage(_localizer["ShowEditLabFormAsync:MissingParameter"], StatusMessageTypes.Error);
                return await RenderLabListAsync();
            }

            return await RenderAsync(ViewType.Edit, lab);
        }

        [HttpGet("edit")]
        public Task<IActionResult> ShowEditLabFormAsync(int id)
        {
            return ShowEditLabFormAsync(id, null);
        }

        [HttpPost("edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditLabAsync(Lab labData)
        {
            // Check input
            if(!ModelState.IsValid)
            {
                AddStatusMessage(_localizer["EditLabAsync:InvalidInput"], StatusMessageTypes.Error);
                return await ShowEditLabFormAsync(null, labData);
            }

            try
            {
                // Retrieve edited lab from database and apply changes
                var lab = await _labService.GetLabAsync(labData.Id, HttpContext.RequestAborted);
                lab.Name = labData.Name;
                lab.ApiCode = labData.ApiCode;
                lab.ServerBaseUrl = labData.ServerBaseUrl;
                lab.MaxFlagPoints = labData.MaxFlagPoints;
                await _labService.UpdateLabAsync(lab, HttpContext.RequestAborted);

                AddStatusMessage(_localizer["EditLabAsync:Success"], StatusMessageTypes.Success);
            }
            catch(InvalidOperationException ex)
            {
                _logger.LogError(ex, "Edit lab");
                AddStatusMessage(_localizer["EditLabAsync:UnknownError"], StatusMessageTypes.Error);
                return await ShowEditLabFormAsync(null, labData);
            }

            return await RenderLabListAsync();
        }

        [HttpGet("create")]
        public async Task<IActionResult> ShowCreateLabFormAsync(Lab lab = null)
        {
            return await RenderAsync(ViewType.Create, lab);
        }

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLabAsync(Lab labData)
        {
            // Check input
            if(!ModelState.IsValid)
            {
                AddStatusMessage(_localizer["CreateLabAsync:InvalidInput"], StatusMessageTypes.Error);
                return await ShowCreateLabFormAsync(labData);
            }

            try
            {
                // Create lab
                var lab = new Lab
                {
                    Name = labData.Name,
                    ApiCode = labData.ApiCode,
                    ServerBaseUrl = labData.ServerBaseUrl,
                    MaxFlagPoints = labData.MaxFlagPoints
                };
                await _labService.CreateLabAsync(lab, HttpContext.RequestAborted);

                AddStatusMessage(_localizer["CreateLabAsync:Success"], StatusMessageTypes.Success);
            }
            catch(InvalidOperationException ex)
            {
                _logger.LogError(ex, "Create lab");
                AddStatusMessage(_localizer["CreateLabAsync:UnknownError"], StatusMessageTypes.Error);
                return await ShowCreateLabFormAsync(labData);
            }

            return await RenderLabListAsync();
        }

        [HttpPost("delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteLabAsync(int id)
        {
            // Input check
            var lab = await _labService.GetLabAsync(id, HttpContext.RequestAborted);
            if(lab == null)
            {
                AddStatusMessage(_localizer["DeleteLabAsync:NotFound"], StatusMessageTypes.Error);
                return await RenderLabListAsync();
            }

            if(lab.Executions.Any())
            {
                AddStatusMessage(_localizer["DeleteLabAsync:HasExecutions"], StatusMessageTypes.Error);
                return await RenderLabListAsync();
            }

            try
            {
                // Delete lab
                await _labService.DeleteLabAsync(id, HttpContext.RequestAborted);

                AddStatusMessage(_localizer["DeleteLabAsync:Success"], StatusMessageTypes.Success);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Delete lab");
                AddStatusMessage(_localizer["DeleteLabAsync:UnknownError"], StatusMessageTypes.Error);
            }

            return await RenderLabListAsync();
        }

        public enum ViewType
        {
            List,
            Edit,
            Create
        }
    }
}
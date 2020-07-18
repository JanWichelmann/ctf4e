using System;
using System.Linq;
using System.Threading.Tasks;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Models;
using Ctf4e.Server.Options;
using Ctf4e.Server.Services;
using Ctf4e.Utilities;
using Ctf4e.Utilities.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ctf4e.Server.Controllers
{
    [Route("admin/flags")]
    [Authorize(Policy = AuthenticationStrings.PolicyIsAdmin)]
    public class AdminFlagsController : ControllerBase
    {
        private readonly IFlagService _flagService;
        private readonly ILabService _labService;

        public AdminFlagsController(IUserService userService, IOptions<MainOptions> mainOptions, IFlagService flagService, ILabService labService)
            : base(userService, mainOptions, "~/Views/AdminFlags.cshtml")
        {
            _flagService = flagService ?? throw new ArgumentNullException(nameof(flagService));
            _labService = labService ?? throw new ArgumentNullException(nameof(labService));
        }

        private async Task<IActionResult> RenderAsync(ViewType viewType, int labId, object model = null)
        {
            var lab = await _labService.GetLabAsync(labId, HttpContext.RequestAborted);
            if(lab == null)
                return this.RedirectToAction<AdminLabsController>(nameof(AdminLabsController.RenderLabListAsync));
            ViewData["Lab"] = await _labService.GetLabAsync(labId, HttpContext.RequestAborted);

            ViewData["ViewType"] = viewType;
            return await RenderViewAsync(MenuItems.AdminFlags, model);
        }

        [HttpGet]
        public async Task<IActionResult> RenderFlagListAsync(int labId)
        {
            ViewData["Flags"] = await _flagService.GetFlagsAsync(labId).ToListAsync();

            return await RenderAsync(ViewType.ListFlags, labId);
        }

        private async Task<IActionResult> ShowEditFlagFormAsync(int? id, Flag flag = null)
        {
            // Retrieve by ID, if no object from a failed POST was passed
            if(id != null)
            {
                flag = await _flagService.GetFlagAsync(id.Value, HttpContext.RequestAborted);
                if(flag == null)
                    return this.RedirectToAction<AdminLabsController>(nameof(AdminLabsController.RenderLabListAsync));
            }
            if(flag == null)
                return this.RedirectToAction<AdminLabsController>(nameof(AdminLabsController.RenderLabListAsync));

            return await RenderAsync(ViewType.EditFlag, flag.LabId, flag);
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
                AddStatusMessage("Ungültige Eingabe.", StatusMessageTypes.Error);
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

                AddStatusMessage("Änderungen gespeichert.", StatusMessageTypes.Success);
                return await RenderFlagListAsync(flag.LabId);
            }
            catch(InvalidOperationException ex)
            {
                AddStatusMessage("Fehler: " + ex.Message, StatusMessageTypes.Error);
                return await ShowEditFlagFormAsync(null, flagData);
            }
        }

        [HttpGet("create")]
        public async Task<IActionResult> ShowCreateFlagFormAsync(int labId, Flag flag = null)
        {
            return await RenderAsync(ViewType.CreateFlag, labId, flag);
        }

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFlagAsync(Flag flagData)
        {
            // Check input
            if(!ModelState.IsValid)
            {
                AddStatusMessage("Ungültige Eingabe.", StatusMessageTypes.Error);
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

                AddStatusMessage("Die Flag wurde erfolgreich erstellt.", StatusMessageTypes.Success);
                return await RenderFlagListAsync(flagData.LabId);
            }
            catch(InvalidOperationException ex)
            {
                AddStatusMessage("Fehler: " + ex.Message, StatusMessageTypes.Error);
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
                return this.RedirectToAction<AdminLabsController>(nameof(AdminLabsController.RenderLabListAsync));

            try
            {
                // Delete flag
                await _flagService.DeleteFlagAsync(id, HttpContext.RequestAborted);

                AddStatusMessage("Die Flag wurde erfolgreich gelöscht.", StatusMessageTypes.Success);
            }
            catch(Exception ex)
            {
                AddStatusMessage("Fehler: " + ex, StatusMessageTypes.Error);
            }

            return await RenderFlagListAsync(flag.LabId);
        }

        public enum ViewType
        {
            ListFlags,
            EditFlag,
            CreateFlag
        }
    }
}

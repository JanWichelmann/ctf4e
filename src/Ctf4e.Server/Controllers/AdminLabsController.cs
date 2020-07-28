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
using Microsoft.Extensions.Options;

namespace Ctf4e.Server.Controllers
{
    [Route("admin/labs")]
    [Authorize(Policy = AuthenticationStrings.PolicyIsAdmin)]
    public class AdminLabsController : ControllerBase
    {
        private readonly ILabService _labService;

        public AdminLabsController(IUserService userService, IOptions<MainOptions> mainOptions, ILabService labService)
            : base(userService, mainOptions, "~/Views/AdminLabs.cshtml")
        {
            _labService = labService ?? throw new ArgumentNullException(nameof(labService));
        }

        private Task<IActionResult> RenderAsync(ViewType viewType, object model = null)
        {
            ViewData["ViewType"] = viewType;
            return RenderViewAsync(MenuItems.AdminLabs, model);
        }

        [HttpGet]
        public async Task<IActionResult> RenderLabListAsync()
        {
            // Pass labs
            ViewData["Labs"] = await _labService.GetFullLabsAsync().ToListAsync();

            return await RenderAsync(ViewType.ListLabs);
        }

        private async Task<IActionResult> ShowEditLabFormAsync(int? id, Lab lab = null)
        {
            // Retrieve by ID, if no object from a failed POST was passed
            if(id != null)
            {
                lab = await _labService.GetLabAsync(id.Value, HttpContext.RequestAborted);
                if(lab == null)
                {
                    AddStatusMessage("Dieses Praktikum existiert nicht.", StatusMessageTypes.Error);
                    return await RenderLabListAsync();
                }
            }
            if(lab == null)
            {
                AddStatusMessage("Kein Praktikum übergeben.", StatusMessageTypes.Error);
                return await RenderLabListAsync();
            }

            return await RenderAsync(ViewType.EditLab, lab);
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
                AddStatusMessage("Ungültige Eingabe.", StatusMessageTypes.Error);
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

                AddStatusMessage("Änderungen gespeichert.", StatusMessageTypes.Success);
            }
            catch(InvalidOperationException ex)
            {
                AddStatusMessage("Fehler: " + ex.Message, StatusMessageTypes.Error);
                return await ShowEditLabFormAsync(null, labData);
            }

            return await RenderLabListAsync();
        }

        [HttpGet("create")]
        public async Task<IActionResult> ShowCreateLabFormAsync(Lab lab = null)
        {
            return await RenderAsync(ViewType.CreateLab, lab);
        }

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLabAsync(Lab labData)
        {
            // Check input
            if(!ModelState.IsValid)
            {
                AddStatusMessage("Ungültige Eingabe.", StatusMessageTypes.Error);
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

                AddStatusMessage("Das Praktikum wurde erfolgreich erstellt.", StatusMessageTypes.Success);
            }
            catch(InvalidOperationException ex)
            {
                AddStatusMessage("Fehler: " + ex.Message, StatusMessageTypes.Error);
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
                AddStatusMessage("Dieses Praktikum existiert nicht.", StatusMessageTypes.Error);
                return await RenderLabListAsync();
            }
            if(lab.Executions.Any())
            {
                AddStatusMessage("Das Praktikum hat bereits einmal stattgefunden und kann somit nicht mehr gelöscht werden.", StatusMessageTypes.Error);
                return await RenderLabListAsync();
            }

            try
            {
                // Delete lab
                await _labService.DeleteLabAsync(id, HttpContext.RequestAborted);

                AddStatusMessage("Das Praktikum wurde erfolgreich gelöscht.", StatusMessageTypes.Success);
            }
            catch(Exception ex)
            {
                AddStatusMessage("Fehler: " + ex, StatusMessageTypes.Error);
            }

            return await RenderLabListAsync();
        }

        public enum ViewType
        {
            ListLabs,
            EditLab,
            CreateLab
        }
    }
}

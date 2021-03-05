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
    [Route("admin/flags")]
    [Authorize(Policy = AuthenticationStrings.PolicyIsAdmin)]
    public class AdminFlagsController : ControllerBase
    {
        private readonly IFlagService _flagService;
        private readonly ILessonService _lessonService;

        public AdminFlagsController(IUserService userService, IOptions<MainOptions> mainOptions, IFlagService flagService, ILessonService lessonService)
            : base("~/Views/AdminFlags.cshtml", userService, mainOptions)
        {
            _flagService = flagService ?? throw new ArgumentNullException(nameof(flagService));
            _lessonService = lessonService ?? throw new ArgumentNullException(nameof(lessonService));
        }

        private async Task<IActionResult> RenderAsync(ViewType viewType, int lessonId, object model)
        {
            var lesson = await _lessonService.GetLessonAsync(lessonId, HttpContext.RequestAborted);
            if(lesson == null)
                return this.RedirectToAction("RenderLessonList", "AdminLessons");
            ViewData["Lesson"] = await _lessonService.GetLessonAsync(lessonId, HttpContext.RequestAborted);

            ViewData["ViewType"] = viewType;
            return await RenderViewAsync(MenuItems.AdminFlags, model);
        }

        [HttpGet]
        public async Task<IActionResult> RenderFlagListAsync(int lessonId)
        {
            var flags = await _flagService.GetFlagsAsync(lessonId).ToListAsync();

            return await RenderAsync(ViewType.List, lessonId, flags);
        }

        private async Task<IActionResult> ShowEditFlagFormAsync(int? id, Flag flag = null)
        {
            // Retrieve by ID, if no object from a failed POST was passed
            if(id != null)
            {
                flag = await _flagService.GetFlagAsync(id.Value, HttpContext.RequestAborted);
                if(flag == null)
                    return this.RedirectToAction("RenderLessonList", "AdminLessons");
            }

            if(flag == null)
                return this.RedirectToAction("RenderLessonList", "AdminLessons");

            return await RenderAsync(ViewType.Edit, flag.LessonId, flag);
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
                return await RenderFlagListAsync(flag.LessonId);
            }
            catch(InvalidOperationException ex)
            {
                AddStatusMessage(ex.Message, StatusMessageTypes.Error);
                return await ShowEditFlagFormAsync(null, flagData);
            }
        }

        [HttpGet("create")]
        public async Task<IActionResult> ShowCreateFlagFormAsync(int lessonId, Flag flag = null)
        {
            return await RenderAsync(ViewType.Create, lessonId, flag);
        }

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFlagAsync(Flag flagData)
        {
            // Check input
            if(!ModelState.IsValid)
            {
                AddStatusMessage("Ungültige Eingabe.", StatusMessageTypes.Error);
                return await ShowCreateFlagFormAsync(flagData.LessonId, flagData);
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
                    LessonId = flagData.LessonId
                };
                await _flagService.CreateFlagAsync(flag, HttpContext.RequestAborted);

                AddStatusMessage("Die Flag wurde erfolgreich erstellt.", StatusMessageTypes.Success);
                return await RenderFlagListAsync(flagData.LessonId);
            }
            catch(InvalidOperationException ex)
            {
                AddStatusMessage(ex.Message, StatusMessageTypes.Error);
                return await ShowCreateFlagFormAsync(flagData.LessonId, flagData);
            }
        }

        [HttpPost("delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFlagAsync(int id)
        {
            // Input check
            var flag = await _flagService.GetFlagAsync(id, HttpContext.RequestAborted);
            if(flag == null)
                return this.RedirectToAction("RenderLessonList", "AdminLessons");

            try
            {
                // Delete flag
                await _flagService.DeleteFlagAsync(id, HttpContext.RequestAborted);

                AddStatusMessage("Die Flag wurde erfolgreich gelöscht.", StatusMessageTypes.Success);
            }
            catch(Exception ex)
            {
                AddStatusMessage(ex.ToString(), StatusMessageTypes.Error);
            }

            return await RenderFlagListAsync(flag.LessonId);
        }

        public enum ViewType
        {
            List,
            Edit,
            Create
        }
    }
}
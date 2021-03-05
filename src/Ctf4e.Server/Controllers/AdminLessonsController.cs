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
    [Route("admin/lessons")]
    [Authorize(Policy = AuthenticationStrings.PolicyIsAdmin)]
    public class AdminLessonsController : ControllerBase
    {
        private readonly ILessonService _lessonService;

        public AdminLessonsController(IUserService userService, IOptions<MainOptions> mainOptions, ILessonService lessonService)
            : base("~/Views/AdminLessons.cshtml", userService, mainOptions)
        {
            _lessonService = lessonService ?? throw new ArgumentNullException(nameof(lessonService));
        }

        private Task<IActionResult> RenderAsync(ViewType viewType, object model)
        {
            ViewData["ViewType"] = viewType;
            
            return RenderViewAsync(MenuItems.AdminLessons, model);
        }

        [HttpGet]
        public async Task<IActionResult> RenderLessonListAsync()
        {
            // Pass lessons
            var lessons = await _lessonService.GetFullLessonsAsync().ToListAsync();

            return await RenderAsync(ViewType.List, lessons);
        }

        private async Task<IActionResult> ShowEditLessonFormAsync(int? id, Lesson lesson = null)
        {
            // Retrieve by ID, if no object from a failed POST was passed
            if(id != null)
            {
                lesson = await _lessonService.GetLessonAsync(id.Value, HttpContext.RequestAborted);
                if(lesson == null)
                {
                    AddStatusMessage("Dieses Praktikum existiert nicht.", StatusMessageTypes.Error);
                    return await RenderLessonListAsync();
                }
            }

            if(lesson == null)
            {
                AddStatusMessage("Kein Praktikum übergeben.", StatusMessageTypes.Error);
                return await RenderLessonListAsync();
            }

            return await RenderAsync(ViewType.Edit, lesson);
        }

        [HttpGet("edit")]
        public Task<IActionResult> ShowEditLessonFormAsync(int id)
        {
            return ShowEditLessonFormAsync(id, null);
        }

        [HttpPost("edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditLessonAsync(Lesson lessonData)
        {
            // Check input
            if(!ModelState.IsValid)
            {
                AddStatusMessage("Ungültige Eingabe.", StatusMessageTypes.Error);
                return await ShowEditLessonFormAsync(null, lessonData);
            }

            try
            {
                // Retrieve edited lesson from database and apply changes
                var lesson = await _lessonService.GetLessonAsync(lessonData.Id, HttpContext.RequestAborted);
                lesson.Name = lessonData.Name;
                lesson.ApiCode = lessonData.ApiCode;
                lesson.ServerBaseUrl = lessonData.ServerBaseUrl;
                lesson.MaxFlagPoints = lessonData.MaxFlagPoints;
                await _lessonService.UpdateLessonAsync(lesson, HttpContext.RequestAborted);

                AddStatusMessage("Änderungen gespeichert.", StatusMessageTypes.Success);
            }
            catch(InvalidOperationException ex)
            {
                AddStatusMessage(ex.Message, StatusMessageTypes.Error);
                return await ShowEditLessonFormAsync(null, lessonData);
            }

            return await RenderLessonListAsync();
        }

        [HttpGet("create")]
        public async Task<IActionResult> ShowCreateLessonFormAsync(Lesson lesson = null)
        {
            return await RenderAsync(ViewType.Create, lesson);
        }

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLessonAsync(Lesson lessonData)
        {
            // Check input
            if(!ModelState.IsValid)
            {
                AddStatusMessage("Ungültige Eingabe.", StatusMessageTypes.Error);
                return await ShowCreateLessonFormAsync(lessonData);
            }

            try
            {
                // Create lesson
                var lesson = new Lesson
                {
                    Name = lessonData.Name,
                    ApiCode = lessonData.ApiCode,
                    ServerBaseUrl = lessonData.ServerBaseUrl,
                    MaxFlagPoints = lessonData.MaxFlagPoints
                };
                await _lessonService.CreateLessonAsync(lesson, HttpContext.RequestAborted);

                AddStatusMessage("Das Praktikum wurde erfolgreich erstellt.", StatusMessageTypes.Success);
            }
            catch(InvalidOperationException ex)
            {
                AddStatusMessage(ex.Message, StatusMessageTypes.Error);
                return await ShowCreateLessonFormAsync(lessonData);
            }

            return await RenderLessonListAsync();
        }

        [HttpPost("delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteLessonAsync(int id)
        {
            // Input check
            var lesson = await _lessonService.GetLessonAsync(id, HttpContext.RequestAborted);
            if(lesson == null)
            {
                AddStatusMessage("Dieses Praktikum existiert nicht.", StatusMessageTypes.Error);
                return await RenderLessonListAsync();
            }

            if(lesson.Executions.Any())
            {
                AddStatusMessage("Das Praktikum hat bereits einmal stattgefunden und kann somit nicht mehr gelöscht werden.", StatusMessageTypes.Error);
                return await RenderLessonListAsync();
            }

            try
            {
                // Delete lesson
                await _lessonService.DeleteLessonAsync(id, HttpContext.RequestAborted);

                AddStatusMessage("Das Praktikum wurde erfolgreich gelöscht.", StatusMessageTypes.Success);
            }
            catch(Exception ex)
            {
                AddStatusMessage(ex.ToString(), StatusMessageTypes.Error);
            }

            return await RenderLessonListAsync();
        }

        public enum ViewType
        {
            List,
            Edit,
            Create
        }
    }
}
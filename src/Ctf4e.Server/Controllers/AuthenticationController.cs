using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Models;
using Ctf4e.Server.Options;
using Ctf4e.Server.Services;
using Ctf4e.Server.ViewModels;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Ctf4e.Server.Controllers
{
    [Route("auth")]
    public partial class AuthenticationController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ISlotService _slotService;
        private readonly IConfigurationService _configurationService;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IServiceProvider _serviceProvider;

        public AuthenticationController(IUserService userService, IOptions<MainOptions> mainOptions, ISlotService slotService, IConfigurationService configurationService, IWebHostEnvironment webHostEnvironment, IServiceProvider serviceProvider)
            : base("~/Views/Authentication.cshtml", userService, mainOptions)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _slotService = slotService ?? throw new ArgumentNullException(nameof(slotService));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _webHostEnvironment = webHostEnvironment ?? throw new ArgumentNullException(nameof(webHostEnvironment));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        private Task<IActionResult> RenderAsync(ViewType viewType, object model = null)
        {
            ViewData["ViewType"] = viewType;
            return RenderViewAsync(MenuItems.Authentication, model);
        }

        [HttpGet]
        public async Task<IActionResult> RenderAsync()
        {
            // Logged in?
            var currentUser = await GetCurrentUserAsync();
            if(currentUser == null)
            {
                AddStatusMessage("Ein Login ist nur über den Moodlekurs möglich.", StatusMessageTypes.Info);
                return await RenderAsync(ViewType.Blank);
            }

            // Assigned to group?
            if(currentUser.Group == null)
                return RedirectToAction("ShowGroupForm", "Authentication"); // Cookie is already set, so redirection is safe
            return await RenderAsync(ViewType.Redirect);
        }

#if DEBUG
        [HttpGet("login/dev/{userId}")]
        public async Task<IActionResult> DevLoginAsync(int userId)
        {
            // Already logged in?
            var currentUser = await GetCurrentUserAsync();
            if(currentUser != null)
                return await RenderAsync(ViewType.Redirect);

            // Find user
            var user = await _userService.GetUserAsync(userId, HttpContext.RequestAborted);
            if(user == null)
            {
                AddStatusMessage("Dieser Benutzer existiert nicht.", StatusMessageTypes.Error);
                return await RenderAsync(ViewType.Blank);
            }

            // Sign in user
            await DoLoginAsync(user);

            // Done
            AddStatusMessage("Login erfolgreich!", StatusMessageTypes.Success);
            if(user.Group == null)
                return await ShowGroupFormAsync();
            return await RenderAsync(ViewType.Redirect);
        }
#endif

        [HttpGet("login/as")]
        [Authorize(Policy = AuthenticationStrings.PolicyIsAdmin)]
        public async Task<IActionResult> AdminLoginAsUserAsync(int userId)
        {
            // Only allow this in development mode
            if(!_webHostEnvironment.IsDevelopment())
                return Forbid();
            
            // Try to retrieve user
            var user = await _userService.GetUserAsync(userId, HttpContext.RequestAborted);
            if(user == null)
            {
                AddStatusMessage("Dieser Benutzer existiert nicht.", StatusMessageTypes.Error);
                return await RenderAsync(ViewType.Blank);
            }

            // Sign in again, as given user
            await DoLoginAsync(user);

            // Done
            AddStatusMessage($"Login als {user.DisplayName} erfolgreich!", StatusMessageTypes.Success);
            if(user.Group == null)
                return await ShowGroupFormAsync();
            return await RenderAsync(ViewType.Redirect);
        }

        private async Task DoLoginAsync(User user)
        {
            // Prepare session data to identify user
            var claims = new List<Claim>
            {
                new Claim(AuthenticationStrings.ClaimUserId, user.Id.ToString())
            };
            if(user.IsAdmin)
                claims.Add(new Claim(AuthenticationStrings.ClaimIsAdmin, true.ToString()));
            if(user.IsTutor)
                claims.Add(new Claim(AuthenticationStrings.ClaimIsTutor, true.ToString()));
            if(user.GroupId != null)
                claims.Add(new Claim(AuthenticationStrings.ClaimIsGroupMember, true.ToString()));

            var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
            var authProperties = new AuthenticationProperties
            {
                AllowRefresh = true,
                IsPersistent = true
            };

            // Login
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal, authProperties);

            // Make sure the current user is set correctly
            await HandleUserLoginAsync(user.Id);
        }

        [HttpGet("logout")]
        [Authorize]
        public async Task<IActionResult> LogoutAsync()
        {
            // Logout
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Make sure the current user is set correctly
            HandleUserLogout();

            // Done
            AddStatusMessage("Logout erfolgreich.", StatusMessageTypes.Success);
            return await RenderAsync(ViewType.Blank);
        }

        private async Task<IActionResult> ShowGroupFormAsync(GroupSelection groupSelection)
        {
            // Does the user already have a group?
            var currentUser = await GetCurrentUserAsync();
            if(currentUser.Group != null)
                return this.RedirectToAction("RenderScoreboard", "Scoreboard");

            // Pass slots
            ViewData["Slots"] = await _slotService.GetSlotsAsync().ToListAsync();

            return await RenderAsync(ViewType.GroupSelection, groupSelection);
        }

        [HttpGet("selgroup")]
        [Authorize]
        public Task<IActionResult> ShowGroupFormAsync()
        {
            return ShowGroupFormAsync(null);
        }

        [HttpPost("selgroup")]
        [Authorize]
        public async Task<IActionResult> HandleGroupSelectionAsync(GroupSelection groupSelection)
        {
            // Some input validation
            if(!ModelState.IsValid)
            {
                AddStatusMessage("Ungültige Eingabe.", StatusMessageTypes.Error);
                return await ShowGroupFormAsync(groupSelection);
            }

            // Try to create group
            var currentUser = await GetCurrentUserAsync();
            try
            {
                // Filter group codes
                var groupSizeMin = await _configurationService.GetGroupSizeMinAsync(HttpContext.RequestAborted);
                var groupSizeMax = await _configurationService.GetGroupSizeMaxAsync(HttpContext.RequestAborted);
                var codes = groupSelection.OtherUserCodes
                    .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Append(currentUser.GroupFindingCode)
                    .Select(c => c.Trim())
                    .Distinct()
                    .ToList();
                if(codes.Count < groupSizeMin)
                {
                    AddStatusMessage($"Es wurden zu wenig Codes eingegeben. Die minimale Gruppengröße beträgt {groupSizeMin}.", StatusMessageTypes.Error);
                    return await ShowGroupFormAsync(groupSelection);
                }
                if(codes.Count > groupSizeMax)
                {
                    AddStatusMessage($"Es wurden zu viele Codes eingegeben. Die maximale Gruppengröße beträgt {groupSizeMax}.", StatusMessageTypes.Error);
                    return await ShowGroupFormAsync(groupSelection);
                }
                
                // Create group
                // The service method will do further error checking (i.e., validity of codes, whether users are already in a group, ...)
                var group = new Group
                {
                    DisplayName = groupSelection.DisplayName,
                    SlotId = groupSelection.SlotId,
                    ShowInScoreboard = groupSelection.ShowInScoreboard
                };
                await _userService.CreateGroupAsync(group, codes, HttpContext.RequestAborted);
            }
            catch(ArgumentException)
            {
                AddStatusMessage("Ungültige Eingabe.", StatusMessageTypes.Error);
                return await ShowGroupFormAsync(groupSelection);
            }
            catch(InvalidOperationException)
            {
                AddStatusMessage("Mindestens ein eingegebener Code ist bereits einer Gruppe zugewiesen.", StatusMessageTypes.Error);
                return await ShowGroupFormAsync(groupSelection);
            }
            catch
            {
                // Should only happen on larger database failures or when users mess around with the input model
                AddStatusMessage("Ein Fehler ist aufgetreten.", StatusMessageTypes.Error);
                return await ShowGroupFormAsync(groupSelection);
            }

            // Success
            AddStatusMessage("Gruppeneintragung erfolgreich.", StatusMessageTypes.Success);
            AddStatusMessage("Bitte loggen Sie sich erneut über den Moodlekurs ein, um die Registrierung abzuschließen.", StatusMessageTypes.Info);
            return await LogoutAsync();
        }

        public enum ViewType
        {
            Blank,
            GroupSelection,
            Redirect
        }
    }
}
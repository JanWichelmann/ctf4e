using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Ctf4e.Server.Attributes;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Models;
using Ctf4e.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Ctf4e.Server.Controllers
{
    /// <summary>
    ///     Abstract base class for controllers.
    /// </summary>
    public abstract class ControllerBase : Utilities.Controllers.ControllerBase
    {
        /// <summary>
        ///     Version of this assembly.
        /// </summary>
        private static string _buildId = null;

        private readonly IUserService _userService;
        private User _currentUser = null;
        private bool _currentUserHasLoggedOut = false;

        protected ControllerBase(string viewPath, IUserService userService)
            : base(viewPath)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));

            if(_buildId == null)
            {
                _buildId = Assembly.GetExecutingAssembly()
                    .GetCustomAttributes<AssemblyBuildVersionAttribute>()
                    .FirstOrDefault()?.Version ?? "<empty>";
            }
        }

        /// <summary>
        /// Updates the internal current user variable.
        /// Internal method, only to be called on login due to the still unset session variable.
        /// </summary>
        /// <param name="userId">The ID of the currently logged in user.</param>
        /// <returns></returns>
        protected async Task HandleUserLoginAsync(int userId)
        {
            _currentUser = await _userService.GetUserAsync(userId, HttpContext.RequestAborted);
        }

        /// <summary>
        /// Clears the internal current user variable.
        /// Internal method, only to be called on logout due to the still set session variable.
        /// </summary>
        /// <returns></returns>
        protected void HandleUserLogout()
        {
            _currentUser = null;
            _currentUserHasLoggedOut = true;
        }

        /// <summary>
        /// Updates the internal current user variable.
        /// </summary>
        /// <returns></returns>
        private async Task ReadCurrentUserFromSessionAsync()
        {
            // User authenticated?
            bool isAuthenticated = User.Identities.Any(i => i.IsAuthenticated);
            if(isAuthenticated)
            {
                // Retrieve user data
                int userId = int.Parse(User.Claims.First(c => c.Type == AuthenticationStrings.ClaimUserId).Value);
                _currentUser = await _userService.GetUserAsync(userId, HttpContext.RequestAborted);
            }
        }

        /// <summary>
        /// Returns the user data of the currently authenticated user.
        /// </summary>
        /// <returns></returns>
        protected async Task<User> GetCurrentUserAsync()
        {
            // Cached user data?
            if(_currentUser != null || _currentUserHasLoggedOut)
                return _currentUser;

            await ReadCurrentUserFromSessionAsync();
            return _currentUser;
        }

        /// <summary>
        /// Passes some global variables to the template engine and renders the previously specified view.
        /// </summary>
        /// <param name="activeMenuItem">The page to be shown as "active" in the menu.</param>
        /// <param name="model">The model being shown/edited in this view.</param>
        protected async Task<IActionResult> RenderViewAsync(MenuItems activeMenuItem = MenuItems.Undefined, object model = null)
        {
            // Pass current user
            var currentUser = await GetCurrentUserAsync();
            ViewData["CurrentUser"] = currentUser;

            // Pass active menu item
            ViewData["ActiveMenuItem"] = activeMenuItem;

            // Page title
            // Request service manually, to avoid injecting too many services in constructors of derived classes
            var configService = HttpContext.RequestServices.GetService<IConfigurationService>() ?? throw new Exception("Could not retrieve configuration service.");
            ViewData["PageTitle"] = await configService.GetPageTitleAsync();
            ViewData["NavbarTitle"] = await configService.GetNavbarTitleAsync();

            // Other render data
            ViewData["BuildId"] = _buildId;

            // Render view
            return RenderView(model);
        }
    }
}
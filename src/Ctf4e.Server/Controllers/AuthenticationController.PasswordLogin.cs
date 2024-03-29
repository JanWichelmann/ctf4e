using System.Threading;
using System.Threading.Tasks;
using Ctf4e.Server.Models;
using Ctf4e.Server.Services;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Ctf4e.Server.Controllers;

public partial class AuthenticationController
{
    [HttpPost("login/password")]
    public async Task<IActionResult> PasswordLoginAsync([FromServices] ILoginRateLimiter loginRateLimiter, string username, string password, string referer)
    {
        var passwordHasher = new PasswordHasher<User>();

        // Already logged in?
        var currentUser = await GetCurrentUserAsync();
        if(currentUser != null)
            return await ShowRedirectAsync(referer);

        // Find user
        bool success = false;
        var user = await _userService.FindUserByMoodleNameAsync(username, HttpContext.RequestAborted);
        if(user != null)
        {
            // Check rate limit
            if(loginRateLimiter.CheckRateLimitHit(user.Id))
            {
                AddStatusMessage(_localizer["PasswordLoginAsync:RateLimited"], StatusMessageTypes.Error);
                ViewData["LoginFormUsername"] = username;
                ViewData["Referer"] = referer;
                return await RenderAsync(ViewType.Login);
            }

            // Check password
            var verificationResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if(verificationResult == PasswordVerificationResult.Success)
                success = true;
            else if(verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
            {
                // Update password hash
                user.PasswordHash = passwordHasher.HashPassword(user, password);
                await _userService.UpdateUserAsync(user, CancellationToken.None);

                success = true;
            }
        }

        // Return to login form, if the login attempt failed
        if(!success)
        {
            AddStatusMessage(_localizer["PasswordLoginAsync:WrongPassword"], StatusMessageTypes.Error);
            ViewData["LoginFormUsername"] = username;
            ViewData["Referer"] = referer;
            return await RenderAsync(ViewType.Login);
        }

        // Sign in user
        await DoLoginAsync(user);

        // Reset rate limit
        loginRateLimiter.ResetRateLimit(user.Id);

        // Done
        AddStatusMessage(_localizer["PasswordLoginAsync:Success"], StatusMessageTypes.Success);
        return await ShowRedirectAsync(referer);
    }
}
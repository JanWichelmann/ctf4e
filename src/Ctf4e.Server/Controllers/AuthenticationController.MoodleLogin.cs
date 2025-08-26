using System;
using System.Security;
using System.Threading.Tasks;
using Ctf4e.Server.Authorization;
using Ctf4e.Server.Models;
using Ctf4e.Server.Services;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoodleLti;
using MoodleLti.Options;

namespace Ctf4e.Server.Controllers;

public partial class AuthenticationController
{
    [HttpPost("login/moodle")]
    public async Task<IActionResult> LoginMoodleAsync([FromServices] IOptions<MoodleLtiOptions> moodleLtiOptions, [FromServices] ILoginRateLimiter loginRateLimiter)
    {
        // Already logged in?
        var currentUser = await GetCurrentUserAsync();
        if(currentUser != null)
            return await RedirectAsync(null);

        // Parse and check request
        MoodleAuthenticationMessageData authData;
        try
        {
            authData = await MoodleAuthenticationTools.ParseAuthenticationRequestAsync
            (
                Request,
                moodleLtiOptions.Value.OAuthConsumerKey,
                moodleLtiOptions.Value.OAuthSharedSecret,
                HttpContext.RequestServices.GetRequiredService<ILogger<MoodleAuthenticationTools>>()
            );
        }
        catch(SecurityException ex)
        {
            AddStatusMessage(StatusMessageType.Error, Localizer["LoginMoodleAsync:InvalidLogin"]);
            GetLogger().LogWarning(ex, "Invalid Moodle login attempt");
            GetLogger().LogDebug("Current time: {CurrentTime}", DateTime.Now);
            GetLogger().LogDebug("Request scheme: {RequestScheme}", Request.Scheme);
            return await ShowLoginFormAsync(null);
        }

        // Does the user exist already?
        var user = await _userService.FindUserByLtiUserIdAsync(authData.UserId, HttpContext.RequestAborted);
        if(user == null)
        {
            bool firstUser = !await _userService.AnyUsers(HttpContext.RequestAborted);
            var newUser = new User
            {
                DisplayName = authData.FullName,
                MoodleUserId = authData.UserId,
                MoodleName = authData.LoginName,
                PasswordHash = "",
                GroupFindingCode = RandomStringGenerator.GetRandomString(10),
                IsTutor = firstUser,
                Privileges = firstUser ? UserPrivileges.All : UserPrivileges.Default
            };
            user = await _userService.CreateUserAsync(newUser, HttpContext.RequestAborted);
            GetLogger().LogInformation("Created new user {UserId} (Moodle: {MoodleName})", user.Id, user.MoodleName);
        }

        // Sign in user
        await DoLoginAsync(user);

        // Reset rate limit
        loginRateLimiter.ResetRateLimit(user.Id);

        // Done
        PostStatusMessage = new StatusMessage(StatusMessageType.Success, Localizer["LoginMoodleAsync:Success"]) { AutoHide = true };
        return await RedirectAsync(null);
    }
}
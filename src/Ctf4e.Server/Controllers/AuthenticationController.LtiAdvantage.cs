using System;
using System.Threading.Tasks;
using Ctf4e.Server.Authorization;
using Ctf4e.Server.Models;
using Ctf4e.Server.Services;
using Ctf4e.Utilities;
using LtiAdvantageTools;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Ctf4e.Server.Controllers;

public partial class AuthenticationController
{
    [HttpGet("lti/jwks")]
    public async Task<IActionResult> LtiGetJsonWebKeyAsync([FromServices] ILtiLoginService ltiLoginService)
    {
        var jwk = await ltiLoginService.GetPublicJsonWebKeyAsync(HttpContext.RequestAborted);
        return Json(new { keys = new[] { jwk } });
    }

    [HttpPost("lti/login")]
    public IActionResult LtiOidcLogin([FromServices] ILtiLoginService ltiLoginService)
    {
        try
        {
            return Redirect(ltiLoginService.ProcessOidcLoginRequest(HttpContext));
        }
        catch(LtiLoginException ex )
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("lti/launch")]
    public async Task<IActionResult> LtiLaunchAsync([FromServices] ILtiLoginService ltiLoginService, [FromServices]ILoginRateLimiter loginRateLimiter)
    {
        try
        {
            var form = await HttpContext.Request.ReadFormAsync();
            var loginData = await ltiLoginService.ProcessLtiLaunchAsync(form, HttpContext.RequestAborted);

            if(!int.TryParse(loginData.UserId, out int ltiUserId))
                throw new Exception("Could not parse user ID as integer."); // Technically, the LTI spec allows non-integer user IDs. We may eventually need to support them as well.

            var user = await _userService.FindUserByLtiUserIdAsync(ltiUserId, HttpContext.RequestAborted);
            if(user == null)
            {
                bool firstUser = !await _userService.AnyUsers(HttpContext.RequestAborted);
                var newUser = new User
                {
                    DisplayName = loginData.UserDisplayName,
                    MoodleUserId = ltiUserId,
                    MoodleName = loginData.UserIdentifier,
                    PasswordHash = "",
                    GroupFindingCode = RandomStringGenerator.GetRandomString(10),
                    IsTutor = firstUser,
                    Privileges = firstUser ? UserPrivileges.All : UserPrivileges.Default
                };
                user = await _userService.CreateUserAsync(newUser, HttpContext.RequestAborted);
                GetLogger().LogInformation("Created new user {UserId} (LTI 1.3: {MoodleName})", user.Id, user.MoodleName);
            }

            // Sign in user
            await DoLoginAsync(user);

            // Reset rate limit
            loginRateLimiter.ResetRateLimit(user.Id);

            // Done
            PostStatusMessage = new StatusMessage(StatusMessageType.Success, Localizer["LoginMoodleAsync:Success"]) { AutoHide = true };
            return await RedirectAsync(null);
        }
        catch(LtiLoginException ex)
        {
            GetLogger().LogError(ex, "LTI launch failed");
            return BadRequest(ex.Message);
        }
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "LTI launch failed");
            return BadRequest("LTI launch failed.");
        }
    }
}
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Ctf4e.Server.Authorization;
using Ctf4e.Server.Extensions;
using Ctf4e.Server.Models;
using Ctf4e.Server.Options;
using Ctf4e.Server.Services;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

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
    public IActionResult LtiOidcLogin([FromServices] ILtiLoginService ltiLoginService, [FromServices] IOptions<LtiAdvantageOptions> ltiOptions)
    {
        // Retrieves a parameter either from form data or from the query string.
        string GetParam(string name)
        {
            if(HttpContext.Request.HasFormContentType && HttpContext.Request.Form.ContainsKey(name))
                return HttpContext.Request.Form[name].ToString();
            if(HttpContext.Request.Query.ContainsKey(name))
                return HttpContext.Request.Query[name].ToString();
            return string.Empty;
        }

        var iss = GetParam("iss");
        var loginHint = GetParam("login_hint");
        var targetLinkUri = GetParam("target_link_uri");
        var messageHint = GetParam("lti_message_hint");
        var clientId = GetParam("client_id");
        //var deploymentId = GetParam("lti_deployment_id");

        string nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));

        // Some sanity checks
        if(iss != ltiOptions.Value.Platform.Issuer)
            return BadRequest("Invalid issuer.");
        if(string.IsNullOrEmpty(loginHint))
            return BadRequest("Missing login hint.");
        if(string.IsNullOrEmpty(targetLinkUri))
            return BadRequest("Missing target link uri.");

        // Store state for launch request
        string stateId = ltiLoginService.SaveState(nonce, targetLinkUri, loginHint, messageHint, clientId);

        // Build authentication request
        var authParams = new Dictionary<string, string>
        {
            ["scope"] = "openid",
            ["response_type"] = "id_token",
            ["response_mode"] = "form_post",
            ["prompt"] = "none",
            ["client_id"] = string.IsNullOrEmpty(clientId) ? ltiOptions.Value.Tool.ClientId : clientId,
            ["redirect_uri"] = ltiOptions.Value.Tool.LaunchRedirectUri,
            ["login_hint"] = loginHint,
            ["state"] = stateId,
            ["nonce"] = nonce,

            // LTI-specific params echoed back
            ["lti_message_hint"] = string.IsNullOrEmpty(messageHint) ? null : messageHint
        };

        var authUrl = QueryHelpers.AddQueryString(ltiOptions.Value.Platform.AuthorizationEndpoint, authParams);
        return Redirect(authUrl);
    }

    [HttpPost("lti/launch")]
    public async Task<IActionResult> LtiLaunchAsync([FromServices] IServiceProvider serviceProvider)
    {
        var ltiLoginService = serviceProvider.GetRequiredService<ILtiLoginService>();
        var ltiOptions = serviceProvider.GetRequiredService<IOptions<LtiAdvantageOptions>>().Value;
        var loginRateLimiter = serviceProvider.GetRequiredService<ILoginRateLimiter>();
        
        var form = await HttpContext.Request.ReadFormAsync();
        var idToken = form["id_token"].ToString();
        var stateId = form["state"].ToString();

        if(string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(stateId))
            return BadRequest("Missing id_token/state.");

        var loginState = ltiLoginService.ConsumeState(stateId);
        if(loginState == null)
            return BadRequest("Unknown or expired login state.");

        // Get JWKS of platform
        var keys = (await ltiLoginService.GetPlatformJsonWebKeySetAsync(HttpContext.RequestAborted)).GetSigningKeys();

        var validationParams = new TokenValidationParameters
        {
            RequireSignedTokens = true,
            ValidateIssuer = true,
            ValidIssuer = ltiOptions.Platform.Issuer,
            ValidateAudience = true,
            ValidAudience = string.IsNullOrEmpty(loginState.ClientId) ? ltiOptions.Tool.ClientId : loginState.ClientId,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = keys
        };

        var jwtTokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var principal = jwtTokenHandler.ValidateToken(idToken, validationParams, out var securityToken);
            var jwtSecurityToken = securityToken as JwtSecurityToken;

            // Check nonce
            var nonce = principal.FindFirstValue("nonce");
            if(nonce != loginState.Nonce)
                return BadRequest("Invalid nonce.");

            // Validate some LTI claims
            var messageType = principal.FindFirstValue("https://purl.imsglobal.org/spec/lti/claim/message_type");
            var version = principal.FindFirstValue("https://purl.imsglobal.org/spec/lti/claim/version");

            if(messageType != "LtiResourceLinkRequest" || version is null || !version.StartsWith("1.3"))
                return BadRequest("Not an LTI 1.3 Resource Link launch.");

            var deploymentId = principal.FindFirstValue("https://purl.imsglobal.org/spec/lti/claim/deployment_id");
            if(!string.IsNullOrEmpty(ltiOptions.Platform.DeploymentId) && deploymentId != ltiOptions.Platform.DeploymentId)
                return BadRequest("Deployment ID mismatch.");

            // LTI authentication is done. Proceed with normal login
            // Retrieve info about user

            var subject = jwtSecurityToken?.Subject;
            if(subject == null)
                return BadRequest("Missing subject.");
            if(!int.TryParse(subject, out var ltiUserId))
                return BadRequest("Subject is not an integer."); // Note: The specification does not require this to be an integer. We may need to address that eventually

            string userDisplayName = principal.FindFirstValue("name");
            if(userDisplayName == null)
                return BadRequest("Missing display name.");

            // For the identifier, we try three options, in this order: person_sourcedid, user_username, email.
            var lisClaim = principal.FindFirst("https://purl.imsglobal.org/spec/lti/claim/lis");
            var extClaim = principal.FindFirst("https://purl.imsglobal.org/spec/lti/claim/ext");
            if(lisClaim == null || !lisClaim.TryGetJsonProperty("person_sourcedid", out var userIdentifier) || string.IsNullOrWhiteSpace(userIdentifier))
            {
                if(extClaim == null || !extClaim.TryGetJsonProperty("user_username", out userIdentifier) || string.IsNullOrWhiteSpace(userIdentifier))
                {
                    userIdentifier = principal.FindFirstValue("email");
                    if(string.IsNullOrWhiteSpace(userIdentifier))
                        return BadRequest("Could not extract suitable user identifier.");
                }
            }

            var user = await _userService.FindUserByLtiUserIdAsync(ltiUserId, HttpContext.RequestAborted);
            if(user == null)
            {
                bool firstUser = !await _userService.AnyUsers(HttpContext.RequestAborted);
                var newUser = new User
                {
                    DisplayName = userDisplayName,
                    MoodleUserId = ltiUserId,
                    MoodleName = userIdentifier,
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
        catch(Exception ex)
        {
            GetLogger().LogError(ex, "LTI launch failed");
            return BadRequest("LTI launch failed.");
        }
    }
}
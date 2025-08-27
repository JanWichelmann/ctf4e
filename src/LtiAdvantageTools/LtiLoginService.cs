using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LtiAdvantageTools;

public interface ILtiLoginService
{
    Task<JsonWebKeySet> GetPlatformJsonWebKeySetAsync(CancellationToken cancellationToken);
    Task<JsonWebKey> GetPublicJsonWebKeyAsync(CancellationToken cancellationToken);
    string SaveState(string nonce, string targetLinkUri, string loginHint, string messageHint, string clientId);
    LtiLoginService.LoginState? ConsumeState(string stateId);

    /// <summary>
    /// Processes an LTI/OpenIDConnect login request.
    /// </summary>
    /// <param name="httpContext">HTTP context of the request.</param>
    /// <returns>Authentication URL to redirect to.</returns>
    /// <exception cref="InvalidOperationException">Thrown when incomplete configuration is detected.</exception>
    /// <exception cref="LtiLoginException">Thrown when the request is invalid.</exception>
    string ProcessOidcLoginRequest(HttpContext httpContext);

    /// <summary>
    /// Processes an LTI launch request.
    /// </summary>
    /// <param name="form">Form data containing the launch parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Object containing login information.</returns>
    /// <exception cref="InvalidOperationException">Thrown when incomplete configuration is detected.</exception>
    /// <exception cref="LtiLoginException">Thrown when the request is invalid.</exception>
    Task<LtiLoginService.LoginData> ProcessLtiLaunchAsync(IFormCollection form, CancellationToken cancellationToken);
}

public class LtiLoginService(IOptions<LtiAdvantageOptions> ltiOptions, ILtiConfigurationStore configurationStore, IDataProtectionProvider dataProtectionProvider, IHttpClientFactory httpClientFactory) : ILtiLoginService
{
    private static readonly ConcurrentDictionary<string, LoginState> _state = new();

    private static readonly SemaphoreSlim _stateSemaphore = new(1, 1);
    private static JsonWebKey? _publicJsonWebKey;
    private static JsonWebKeySet? _platformJsonWebKeySet;

    private void CheckConfigurationNotNull(string name, string? value)
    {
        if(value == null)   
            throw new InvalidOperationException("Missing configuration value: " + name);
    }

    public async Task<JsonWebKeySet> GetPlatformJsonWebKeySetAsync(CancellationToken cancellationToken)
    {
        if(_platformJsonWebKeySet != null)
            return _platformJsonWebKeySet;

        CheckConfigurationNotNull(nameof(ltiOptions.Value.Platform.JwksUri), ltiOptions.Value.Platform.JwksUri);

        using var httpClient = httpClientFactory.CreateClient();
        _platformJsonWebKeySet = new JsonWebKeySet(await httpClient.GetStringAsync(ltiOptions.Value.Platform.JwksUri, cancellationToken));

        return _platformJsonWebKeySet;
    }

    public async Task<JsonWebKey> GetPublicJsonWebKeyAsync(CancellationToken cancellationToken)
    {
        if(_publicJsonWebKey != null)
            return _publicJsonWebKey;

        // Prevent race conditions from generating inconsistent state
        await _stateSemaphore.WaitAsync(cancellationToken);
        try
        {
            var protector = dataProtectionProvider.CreateProtector("LtiAdvantage.SigningKey");

            string serializedKey = await configurationStore.GetSerializedPublicJsonWebKeyAsync(cancellationToken);
            JsonWebKey privateJwk;
            if(string.IsNullOrEmpty(serializedKey))
            {
                // Create new key
                var rsa = RSA.Create(2048);
                var securityKey = new RsaSecurityKey(rsa)
                {
                    KeyId = Guid.NewGuid().ToString()
                };

                privateJwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(securityKey);
                privateJwk.Use = JsonWebKeyUseNames.Sig;
                privateJwk.Alg = SecurityAlgorithms.RsaSha256;

                serializedKey = protector.Protect(JsonSerializer.Serialize(privateJwk));
                await configurationStore.SetSerializedPublicJsonWebKeyAsync(serializedKey, CancellationToken.None); // Do not allow aborting this operation
            }
            else
            {
                // Deserialize existing key
                privateJwk = new JsonWebKey(protector.Unprotect(serializedKey));
            }

            // Create public key object
            _publicJsonWebKey = new JsonWebKey
            {
                Alg = privateJwk.Alg,
                Use = privateJwk.Use,
                Kid = privateJwk.Kid,
                Kty = privateJwk.Kty,
                KeyOps = { "verify" },
                N = privateJwk.N,
                E = privateJwk.E
            };
            return _publicJsonWebKey;
        }
        finally
        {
            _stateSemaphore.Release();
        }
    }

    public string SaveState(string nonce, string targetLinkUri, string loginHint, string messageHint, string clientId)
    {
        // Clean up expired states
        var toRemove = _state.Where(s => s.Value.ExpiresAt >= DateTime.UtcNow).ToArray();
        foreach(var s in toRemove)
            _state.TryRemove(s);

        // Generate state IDs until we find a non-existing one
        while(true)
        {
            string stateId = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
            if(_state.TryAdd(stateId, new LoginState(nonce, targetLinkUri, loginHint, messageHint, clientId, DateTime.UtcNow.AddMinutes(1))))
                return stateId;
        }
    }

    public LoginState? ConsumeState(string stateId)
    {
        if(!_state.TryGetValue(stateId, out LoginState? state))
            return null;

        _state.TryRemove(stateId, out _);
        return state;
    }

    public string ProcessOidcLoginRequest(HttpContext httpContext)
    {
        CheckConfigurationNotNull(nameof(ltiOptions.Value.Platform.AuthorizationEndpoint), ltiOptions.Value.Platform.AuthorizationEndpoint);
        CheckConfigurationNotNull(nameof(ltiOptions.Value.Platform.Issuer), ltiOptions.Value.Platform.Issuer);
        CheckConfigurationNotNull(nameof(ltiOptions.Value.Tool.ClientId), ltiOptions.Value.Tool.ClientId);
        CheckConfigurationNotNull(nameof(ltiOptions.Value.Tool.LaunchRedirectUri), ltiOptions.Value.Tool.LaunchRedirectUri);
        
        // Retrieves a parameter either from form data or from the query string.
        string GetParam(string name)
        {
            if(httpContext.Request.HasFormContentType && httpContext.Request.Form.TryGetValue(name, out var value))
                return value.ToString();
            if(httpContext.Request.Query.TryGetValue(name, out value))
                return value.ToString();
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
            throw new LtiLoginException("Invalid issuer.");
        if(string.IsNullOrEmpty(loginHint))
            throw new LtiLoginException("Missing login hint.");
        if(string.IsNullOrEmpty(targetLinkUri))
            throw new LtiLoginException("Missing target link uri.");

        // Store state for launch request
        string stateId = SaveState(nonce, targetLinkUri, loginHint, messageHint, clientId);

        // Build authentication request
        var authParams = new Dictionary<string, string?>
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

        var authUrl = QueryHelpers.AddQueryString(ltiOptions.Value.Platform.AuthorizationEndpoint!, authParams);
        return authUrl;
    }

    public async Task<LoginData> ProcessLtiLaunchAsync(IFormCollection form, CancellationToken cancellationToken)
    {
        CheckConfigurationNotNull(nameof(ltiOptions.Value.Platform.Issuer), ltiOptions.Value.Platform.Issuer);
        CheckConfigurationNotNull(nameof(ltiOptions.Value.Platform.DeploymentId), ltiOptions.Value.Platform.DeploymentId);
        CheckConfigurationNotNull(nameof(ltiOptions.Value.Tool.ClientId), ltiOptions.Value.Tool.ClientId);
        
        var idToken = form["id_token"].ToString();
        var stateId = form["state"].ToString();

        if(string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(stateId))
            throw new LtiLoginException("Missing id_token/state.");

        var loginState = ConsumeState(stateId);
        if(loginState == null)
            throw new LtiLoginException("Unknown or expired login state.");

        // Get JWKS of platform
        var keys = (await GetPlatformJsonWebKeySetAsync(cancellationToken)).GetSigningKeys();

        var validationParams = new TokenValidationParameters
        {
            RequireSignedTokens = true,
            ValidateIssuer = true,
            ValidIssuer = ltiOptions.Value.Platform.Issuer,
            ValidateAudience = true,
            ValidAudience = string.IsNullOrEmpty(loginState.ClientId) ? ltiOptions.Value.Tool.ClientId : loginState.ClientId,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = keys
        };

        var jwtTokenHandler = new JwtSecurityTokenHandler();
        var principal = jwtTokenHandler.ValidateToken(idToken, validationParams, out var securityToken);
        var jwtSecurityToken = securityToken as JwtSecurityToken;

        // Check nonce
        var nonce = principal.FindFirstValue("nonce");
        if(nonce != loginState.Nonce)
            throw new LtiLoginException("Invalid nonce.");

        // Validate some LTI claims
        var messageType = principal.FindFirstValue("https://purl.imsglobal.org/spec/lti/claim/message_type");
        var version = principal.FindFirstValue("https://purl.imsglobal.org/spec/lti/claim/version");

        if(messageType != "LtiResourceLinkRequest" || version is null || !version.StartsWith("1.3"))
            throw new LtiLoginException("Not an LTI 1.3 Resource Link launch.");

        var deploymentId = principal.FindFirstValue("https://purl.imsglobal.org/spec/lti/claim/deployment_id");
        if(!string.IsNullOrEmpty(ltiOptions.Value.Platform.DeploymentId) && deploymentId != ltiOptions.Value.Platform.DeploymentId)
            throw new LtiLoginException("Deployment ID mismatch.");

        // LTI authentication is done. Proceed with normal login
        // Retrieve info about user

        var subject = jwtSecurityToken?.Subject;
        if(subject == null)
            throw new LtiLoginException("Missing subject.");

        string userDisplayName = principal.FindFirstValue("name");
        if(userDisplayName == null)
            throw new LtiLoginException("Missing display name.");

        // For the identifier, we try three options, in this order: person_sourcedid, user_username, email.
        var lisClaim = principal.FindFirst("https://purl.imsglobal.org/spec/lti/claim/lis");
        var extClaim = principal.FindFirst("https://purl.imsglobal.org/spec/lti/claim/ext");
        string? userIdentifier;
        if(lisClaim == null || !lisClaim.TryGetJsonProperty("person_sourcedid", out userIdentifier) || string.IsNullOrWhiteSpace(userIdentifier))
        {
            if(extClaim == null || !extClaim.TryGetJsonProperty("user_username", out userIdentifier) || string.IsNullOrWhiteSpace(userIdentifier))
            {
                userIdentifier = principal.FindFirstValue("email");
                if(string.IsNullOrWhiteSpace(userIdentifier))
                    throw new LtiLoginException("Could not extract suitable user identifier.");
            }
        }

        return new LoginData(subject, userIdentifier, userDisplayName);
    }

    [SuppressMessage("ReSharper", "NotAccessedPositionalProperty.Global")]
    public record LoginState(string Nonce, string TargetLinkUri, string LoginHint, string MessageHint, string ClientId, DateTime ExpiresAt);

    /// <summary>
    /// Stores user information after a successful LTI launch.
    /// </summary>
    /// <param name="UserId">User ID, as transmitted by the platform.</param>
    /// <param name="UserIdentifier">Unique user identifier - in decreasing order: person_sourcedid, user_username, email.</param>
    /// <param name="UserDisplayName">User display name.</param>
    [SuppressMessage("ReSharper", "NotAccessedPositionalProperty.Global")]
    public record LoginData(string UserId, string UserIdentifier, string UserDisplayName);
}
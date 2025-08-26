using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ctf4e.Server.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Ctf4e.Server.Services;

public interface ILtiLoginService
{
    Task<JsonWebKeySet> GetPlatformJsonWebKeySetAsync(CancellationToken cancellationToken);
    Task<JsonWebKey> GetPublicJsonWebKeyAsync(CancellationToken cancellationToken);
    string SaveState(string nonce, string targetLinkUri, string loginHint, string messageHint, string clientId);
    LtiLoginService.LoginState ConsumeState(string stateId);
}

public class LtiLoginService(IOptions<LtiAdvantageOptions> ltiOptions, IConfigurationService configurationService, IDataProtectionProvider dataProtectionProvider, IHttpClientFactory httpClientFactory) : ILtiLoginService
{
    private static readonly ConcurrentDictionary<string, LoginState> _state = new();

    private static readonly SemaphoreSlim _stateSemaphore = new(1, 1);
    private static JsonWebKey _publicJsonWebKey;
    private static JsonWebKeySet _platformJsonWebKeySet;

    public async Task<JsonWebKeySet> GetPlatformJsonWebKeySetAsync(CancellationToken cancellationToken)
    {
        if(_platformJsonWebKeySet != null) 
            return _platformJsonWebKeySet;
        
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

            string serializedKey = await configurationService.LtiAdvantageJsonWebKey.GetAsync(cancellationToken);
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
                await configurationService.LtiAdvantageJsonWebKey.SetAsync(serializedKey, CancellationToken.None); // Do not allow aborting this operation
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

    public LoginState ConsumeState(string stateId)
    {
        if(!_state.TryGetValue(stateId, out LoginState state))
            return null;
        
        _state.TryRemove(stateId, out _);
        return state;

    }

    public record LoginState(string Nonce, string TargetLinkUri, string LoginHint, string MessageHint, string ClientId, DateTime ExpiresAt);
}
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Ctf4e.Server.Authorization;

/// <summary>
/// Custom policy provider for user privileges.
/// </summary>
public class UserPrivilegePolicyProvider : IAuthorizationPolicyProvider
{
    private readonly IAuthorizationPolicyProvider _defaultProvider;
    private readonly ConcurrentDictionary<string, AuthorizationPolicy> _policies = new();

    public UserPrivilegePolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _defaultProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public async Task<AuthorizationPolicy> GetPolicyAsync(string policyName)
    {
        // Is the policy already cached?
        if(_policies.TryGetValue(policyName, out var policy))
            return policy;

        // Parse policy
        var policyParts = policyName.Split('-');
        if(policyParts.Length != 3)
            return await _defaultProvider.GetPolicyAsync(policyName);
        
        if(policyParts[0] != AuthenticationStrings.UserPrivilegePolicyPrefix)
            return await _defaultProvider.GetPolicyAsync(policyName);

        if(!int.TryParse(policyParts[1], out int privilegesInt))
            return await _defaultProvider.GetPolicyAsync(policyName);

        var privileges = (UserPrivileges)privilegesInt;

        // Generate requirements
        var policyBuilder = new AuthorizationPolicyBuilder(policyParts[2]);
        policyBuilder.RequireAnyUserPrivileges(privileges);
        policy = policyBuilder.Build();

        // Cache policy
        _policies.TryAdd(policyName, policy);

        return policy;
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return Task.FromResult(
            new AuthorizationPolicyBuilder(CookieAuthenticationDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build()
        );
    }

    public Task<AuthorizationPolicy> GetFallbackPolicyAsync() => Task.FromResult<AuthorizationPolicy>(null);
}
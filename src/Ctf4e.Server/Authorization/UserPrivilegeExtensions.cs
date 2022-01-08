using Microsoft.AspNetCore.Authorization;

namespace Ctf4e.Server.Authorization;

public static class UserPrivilegeExtensions
{
    /// <summary>
    /// Checks whether the user has all of the the given privileges.
    /// </summary>
    /// <param name="authorizationPolicyBuilder">Authorization policy builder.</param>
    /// <param name="requiredPrivileges">The required privileges.</param>
    /// <returns></returns>
    public static AuthorizationPolicyBuilder RequireAllUserPrivileges(this AuthorizationPolicyBuilder authorizationPolicyBuilder, UserPrivileges requiredPrivileges)
    {
        return authorizationPolicyBuilder.AddRequirements(new UserPrivilegeRequirement(requiredPrivileges, false));
    }
        
    /// <summary>
    /// Checks whether the user has any of the the given privileges.
    /// </summary>
    /// <param name="authorizationPolicyBuilder">Authorization policy builder.</param>
    /// <param name="requiredPrivileges">The required privileges.</param>
    /// <returns></returns>
    public static AuthorizationPolicyBuilder RequireAnyUserPrivileges(this AuthorizationPolicyBuilder authorizationPolicyBuilder, UserPrivileges requiredPrivileges)
    {
        return authorizationPolicyBuilder.AddRequirements(new UserPrivilegeRequirement(requiredPrivileges, true));
    }
}
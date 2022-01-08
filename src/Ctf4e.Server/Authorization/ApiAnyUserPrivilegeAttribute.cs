using Microsoft.AspNetCore.Authorization;

namespace Ctf4e.Server.Authorization;

/// <summary>
/// Requires that the user has at least one of the specified privileges (OR).
/// To require multiple privileges (AND), use the attribute several times.
/// The generated policies check against the "Basic" authentication scheme.
/// </summary>
public class ApiAnyUserPrivilegeAttribute : AuthorizeAttribute
{
    public ApiAnyUserPrivilegeAttribute(UserPrivileges privileges)
    {
        Policy = $"{AuthenticationStrings.UserPrivilegePolicyPrefix}-{(int)privileges}-{AuthenticationStrings.BasicAuthenticationScheme}";
        AuthenticationSchemes = AuthenticationStrings.BasicAuthenticationScheme;
    }
}
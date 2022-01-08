using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

namespace Ctf4e.Server.Authorization;

/// <summary>
/// Requires that the user has at least one of the specified privileges (OR).
/// To require multiple privileges (AND), use the attribute several times.
/// The generated policies check against the "Cookie" authentication scheme.
/// </summary>
public class AnyUserPrivilegeAttribute : AuthorizeAttribute
{
    public AnyUserPrivilegeAttribute(UserPrivileges privileges)
    {
        Policy = $"{AuthenticationStrings.UserPrivilegePolicyPrefix}-{(int)privileges}-{CookieAuthenticationDefaults.AuthenticationScheme}";
        AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme;
    }
}
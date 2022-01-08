using Microsoft.AspNetCore.Authorization;

namespace Ctf4e.Server.Authorization;

/// <summary>
/// Denotes one privilege requirement.
/// </summary>
public class UserPrivilegeRequirement : IAuthorizationRequirement
{
    public UserPrivileges RequiredPrivileges { get; }
    public bool Any { get; }

    /// <summary>
    /// Creates a new requirement for user privileges.
    /// </summary>
    /// <param name="requiredPrivileges">The required privileges bitfield.</param>
    /// <param name="any">Sets whether any privilege from <see cref="RequiredPrivileges"/> suffices (true), or whether all privileges must be met (false).</param>
    public UserPrivilegeRequirement(UserPrivileges requiredPrivileges, bool any)
    {
        RequiredPrivileges = requiredPrivileges;
        Any = any;
    }
}
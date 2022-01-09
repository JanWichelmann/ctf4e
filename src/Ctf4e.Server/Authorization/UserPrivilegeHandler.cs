using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ctf4e.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Ctf4e.Server.Authorization;

/// <summary>
/// Allows evaluating the current user's privileges claim for specific privileges.
/// </summary>
public class UserPrivilegeHandler : IAuthorizationHandler
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public UserPrivilegeHandler(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
    }

    public async Task HandleAsync(AuthorizationHandlerContext context)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

        // Retrieve user data
        var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == AuthenticationStrings.ClaimUserId);
        if(userIdClaim == null)
            return;
        int userId = int.Parse(userIdClaim.Value);
        var user = await userService.FindUserByIdAsync(userId, (context.Resource as HttpContext)?.RequestAborted ?? CancellationToken.None);

        // If the user does not exist, deny all access
        if(user == null)
            context.Fail();

        // Check whether all requirements are satisfied
        var pendingRequirements = context.PendingRequirements.ToList();
        foreach(var requirement in pendingRequirements)
        {
            if(requirement is not UserPrivilegeRequirement privilegeRequirement)
                continue;
            
            if(privilegeRequirement.RequiredPrivileges == UserPrivileges.Default)
            {
                context.Succeed(requirement);
                continue;
            }

            if(privilegeRequirement.Any)
            {
                if(user.Privileges.HasAnyPrivilege(privilegeRequirement.RequiredPrivileges))
                    context.Succeed(requirement);
                else
                    context.Fail(new AuthorizationFailureReason(this, "Could not find any of the required privileges"));
            }
            else
            {
                if(user.Privileges.HasPrivileges(privilegeRequirement.RequiredPrivileges))
                    context.Succeed(requirement);
                else
                    context.Fail(new AuthorizationFailureReason(this, "At least one required privilege is missing"));
            }
        }
    }
}
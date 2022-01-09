using System.Threading;
using System.Threading.Tasks;
using Ctf4e.Server.Models;
using Ctf4e.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Ctf4e.Server.Controllers;

public partial class AuthenticationController
{
    [HttpGet("settings")]
    [Authorize]
    public async Task<IActionResult> ShowSettingsFormAsync()
    {
        return await RenderAsync(ViewType.Settings);
    }

    [HttpPost("settings")]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> ChangeSettingsAsync(string oldPassword, string newPassword, string newPassword2)
    {
        var passwordHasher = new PasswordHasher<User>();

        var user = await GetCurrentUserAsync();

        // Update password?
        if(!string.IsNullOrWhiteSpace(newPassword))
        {
            // Check old password
            if(!string.IsNullOrEmpty(user.PasswordHash))
            {
                if(string.IsNullOrWhiteSpace(oldPassword) || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, oldPassword) == PasswordVerificationResult.Failed)
                {
                    AddStatusMessage(_localizer["ChangeSettingsAsync:WrongOldPassword"], StatusMessageTypes.Error);
                    return await RenderAsync(ViewType.Settings);
                }
            }

            // Compare new passwords
            if(newPassword != newPassword2)
            {
                AddStatusMessage(_localizer["ChangeSettingsAsync:PasswordsDoNotMatch"], StatusMessageTypes.Error);
                return await RenderAsync(ViewType.Settings);
            }

            if(newPassword.Length < 8)
            {
                AddStatusMessage(_localizer["ChangeSettingsAsync:WrongPasswordFormat"], StatusMessageTypes.Error);
                return await RenderAsync(ViewType.Settings);
            }

            // Update password hash
            user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
        }

        await _userService.UpdateUserAsync(user, CancellationToken.None);

        // Done
        AddStatusMessage(_localizer["ChangeSettingsAsync:Success"], StatusMessageTypes.Success);
        return await ShowSettingsFormAsync();
    }
}
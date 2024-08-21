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
        return await RenderAsync(ViewType.Settings, "~/Views/Authentication.cshtml");
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
                    AddStatusMessage(StatusMessageType.Error, Localizer["ChangeSettingsAsync:WrongOldPassword"]);
                    return await RenderAsync(ViewType.Settings, "~/Views/Authentication.cshtml");
                }
            }

            // Compare new passwords
            if(newPassword != newPassword2)
            {
                AddStatusMessage(StatusMessageType.Error, Localizer["ChangeSettingsAsync:PasswordsDoNotMatch"]);
                return await RenderAsync(ViewType.Settings, "~/Views/Authentication.cshtml");
            }

            if(newPassword.Length < 8)
            {
                AddStatusMessage(StatusMessageType.Error, Localizer["ChangeSettingsAsync:WrongPasswordFormat"]);
                return await RenderAsync(ViewType.Settings, "~/Views/Authentication.cshtml");
            }

            // Update password hash
            user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
        }

        await _userService.UpdateUserAsync(user, CancellationToken.None);

        // Done
        AddStatusMessage(StatusMessageType.Success, Localizer["ChangeSettingsAsync:Success"]);
        return await ShowSettingsFormAsync();
    }
}
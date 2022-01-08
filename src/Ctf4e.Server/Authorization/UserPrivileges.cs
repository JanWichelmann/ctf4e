using System;

namespace Ctf4e.Server.Authorization;

[Flags]
public enum UserPrivileges
{
    /// <summary>
    /// No privileges.
    /// </summary>
    Default = 0,

    Admin = 1 << 0,
    ViewUsers = 1 << 1,
    EditUsers = 1 << 2,
    ViewGroups = 1 << 3,
    EditGroups = 1 << 4,
    ViewLabs = 1 << 5,
    EditLabs = 1 << 6,
    ViewSlots = 1 << 7,
    EditSlots = 1 << 8,
    ViewLabExecutions = 1 << 9,
    EditLabExecutions = 1 << 10,
    ViewAdminScoreboard = 1 << 11,
    EditAdminScoreboard = 1 << 12,
    EditConfiguration = 1 << 13,
    TransferResults = 1 << 14,
    LoginAsLabServerAdmin = 1 << 15,

    All = Admin
          | ViewUsers | EditUsers
          | ViewGroups | EditGroups
          | ViewLabs | EditLabs
          | ViewSlots | EditSlots
          | ViewLabExecutions | EditLabExecutions
          | ViewAdminScoreboard | EditAdminScoreboard
          | EditConfiguration | TransferResults
          | LoginAsLabServerAdmin
}

public static class UserPrivilegesExtensions
{
    /// <summary>
    /// Returns whether the given privilege value contains specific privileges.
    /// </summary>
    /// <param name="p">Privilege value.</param>
    /// <param name="privileges">Specific privileges to check.</param>
    /// <returns></returns>
    public static bool HasPrivileges(this UserPrivileges p, UserPrivileges privileges)
    {
        return (p & privileges) == privileges;
    }

    /// <summary>
    /// Returns whether the given privilege value contains any of the specified privileges.
    /// </summary>
    /// <param name="p">Privilege value.</param>
    /// <param name="privileges">Privileges to check.</param>
    /// <returns></returns>
    public static bool HasAnyPrivilege(this UserPrivileges p, UserPrivileges privileges)
    {
        return (p & privileges) != 0;
    }
}
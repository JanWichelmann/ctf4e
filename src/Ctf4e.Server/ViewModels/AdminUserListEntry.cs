using Ctf4e.Server.Authorization;

namespace Ctf4e.Server.ViewModels;

public class AdminUserListEntry
{
    public int Id { get; set; }

    public string DisplayName { get; set; }

    public int MoodleUserId { get; set; }
    
    public string MoodleName { get; set; }
        
    public UserPrivileges Privileges { get; set; }
    
    public bool IsTutor { get; set; }

    public string GroupFindingCode { get; set; }

    public int? GroupId { get; set; }
    public string GroupName { get; set; }
}
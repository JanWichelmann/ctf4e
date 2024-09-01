using System.Collections.Generic;
using Ctf4e.Server.Authorization;

namespace Ctf4e.Server.Models;

public class User
{
    public int Id { get; set; }

    public string DisplayName { get; set; }

    public int MoodleUserId { get; set; }
    
    public string MoodleName { get; set; }
        
    public UserPrivileges Privileges { get; set; }
    
    public string PasswordHash { get; set; }
    
    public bool IsTutor { get; set; }

    public string GroupFindingCode { get; set; }

    public ICollection<FlagSubmission> FlagSubmissions { get; set; }

    public ICollection<ExerciseSubmission> ExerciseSubmissions { get; set; }

    public int? GroupId { get; set; }
    public Group Group { get; set; }
}
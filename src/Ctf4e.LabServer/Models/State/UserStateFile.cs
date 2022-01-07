using System.Collections.Generic;

namespace Ctf4e.LabServer.Models.State;

/// <summary>
/// Defines the structure of the user state JSON file.
/// </summary>
public class UserStateFile
{
    public int? GroupId { get; set; }
        
    public string UserName { get; set; }
        
    public string Password { get; set; }
    public List<UserStateFileExerciseEntry> Exercises { get; set; }
}
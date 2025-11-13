using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Ctf4e.LabServer.Models.State;

/// <summary>
/// Contains the state of a user.
/// </summary>
public class UserState
{
    public SemaphoreSlim Lock { get; set; }
        
    public int? GroupId { get; set; }
        
    public List<UserState> GroupMembers { get; set; }
    
    /// <summary>
    /// User ID to use when communicating with container.
    /// Usually matches the CTF user ID, but may be overridden when the CTF system specifies a custom user name of form 'userXX'.
    /// </summary>
    public int DockerUserId { get; set; }
        
    public string UserName { get; set; }
        
    public string Password { get; set; }

    public ConcurrentDictionary<int, UserStateFileExerciseEntry> Exercises { get; set; }
        
    public UserStateLogContainer Log { get; set; }
}
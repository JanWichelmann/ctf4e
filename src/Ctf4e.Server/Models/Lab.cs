using System.Collections.Generic;

namespace Ctf4e.Server.Models;

public class Lab
{
    public int Id { get; set; }

    public string Name { get; set; }

    public string ServerBaseUrl { get; set; }

    public string ApiCode { get; set; }

    public int MaxPoints { get; set; }

    public int MaxFlagPoints { get; set; }
        
    public bool Visible { get; set; }
    
    public int SortIndex { get; set; }

    public ICollection<LabExecution> Executions { get; set; }

    public ICollection<Flag> Flags { get; set; }

    public ICollection<Exercise> Exercises { get; set; }
}
using System.Collections.Generic;

namespace Ctf4e.Server.Models;

public class Flag
{
    public int Id { get; set; }

    public string Code { get; set; }

    public string Description { get; set; }

    public int BasePoints { get; set; }

    public bool IsBounty { get; set; }

    public int LabId { get; set; }

    public Lab Lab { get; set; }

    public ICollection<FlagSubmission> Submissions { get; set; }
}
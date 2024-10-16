using System.ComponentModel.DataAnnotations;
using Ctf4e.Server.Constants;

namespace Ctf4e.Server.ViewModels;

public class GroupSelection
{
    public string OtherUserCodes { get; set; }

    [StringLength(50)]
    public string DisplayName { get; set; }

    [Required]
    public bool ShowInScoreboard { get; set; }

    [Required]
    public int SlotId { get; set; }
}
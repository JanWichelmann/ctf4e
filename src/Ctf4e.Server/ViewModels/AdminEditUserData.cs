using System.ComponentModel.DataAnnotations;

namespace Ctf4e.Server.ViewModels;

public class AdminEditUserData
{
    [Required]
    public int Id { get; set; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    public string DisplayName { get; set; }
    
    public bool IsTutor { get; set; }

    [Required(AllowEmptyStrings = true)]
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [StringLength(100)]
    public string GroupFindingCode { get; set; }

    public int? GroupId { get; set; }
    
    public bool PrivilegeAdmin { get; set; }
    public bool PrivilegeViewUsers { get; set; }
    public bool PrivilegeEditUsers { get; set; }
    public bool PrivilegeViewGroups { get; set; }
    public bool PrivilegeEditGroups { get; set; }
    public bool PrivilegeViewLabs { get; set; }
    public bool PrivilegeEditLabs { get; set; }
    public bool PrivilegeViewSlots { get; set; }
    public bool PrivilegeEditSlots { get; set; }
    public bool PrivilegeViewLabExecutions { get; set; }
    public bool PrivilegeEditLabExecutions { get; set; }
    public bool PrivilegeViewAdminScoreboard { get; set; }
    public bool PrivilegeEditAdminScoreboard { get; set; }
    public bool PrivilegeEditConfiguration { get; set; }
    public bool PrivilegeTransferResults { get; set; }
    public bool PrivilegeLoginAsLabServerAdmin { get; set; }
}
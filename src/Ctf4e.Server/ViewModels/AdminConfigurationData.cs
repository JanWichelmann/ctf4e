using System.ComponentModel.DataAnnotations;

namespace Ctf4e.Server.ViewModels;

public class AdminConfigurationData
{
    [Required]
    public int FlagMinimumPointsDivisor { get; set; }

    [Required]
    public int FlagHalfPointsSubmissionCount { get; set; }

    [Required]
    public int ScoreboardEntryCount { get; set; }

    [Required]
    public int ScoreboardCachedSeconds { get; set; }

    [Required]
    public bool PassAsGroup { get; set; }
    
    [Required]
    public bool ShowGroupMemberSubmissions { get; set; }
    
    [Required]
    public bool EnableScoreboard { get; set; }
    
    [Required]
    public bool EnableFlags { get; set; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(50)]
    public string PageTitle { get; set; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(50)]
    public string NavbarTitle { get; set; }

    [Required]
    public int GroupSizeMin { get; set; }

    [Required]
    public int GroupSizeMax { get; set; }
        
    [Required(AllowEmptyStrings = true)]
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [StringLength(5000)]
    public string GroupSelectionPageText { get; set; }
}
using System.ComponentModel.DataAnnotations;

namespace Ctf4e.Server.ViewModels
{
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

        [Required(AllowEmptyStrings = false)]
        public string PageTitle { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string NavbarTitle { get; set; }

        [Required(AllowEmptyStrings = true)]
        [DisplayFormat(ConvertEmptyStringToNull = false)]
        public string FlagPrefix { get; set; }

        [Required(AllowEmptyStrings = true)]
        [DisplayFormat(ConvertEmptyStringToNull = false)]
        public string FlagSuffix { get; set; }

        [Required]
        public int GroupSizeMin { get; set; }

        [Required]
        public int GroupSizeMax { get; set; }
    }
}
using System.ComponentModel.DataAnnotations;
using LtiLibrary.NetCore.Lti.v2;

namespace Ctf4e.Server.ViewModels
{
    public class AdminConfigurationData
    {
        [Required] public int FlagMinimumPointsDivisor { get; set; }

        [Required] public int FlagHalfPointsSubmissionCount { get; set; }

        [Required] public int ScoreboardEntryCount { get; set; }

        [Required] public int ScoreboardCachedSeconds { get; set; }

        [Required] public bool CreateSplitGroups { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string PageTitle { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string NavbarTitle { get; set; }
    }
}
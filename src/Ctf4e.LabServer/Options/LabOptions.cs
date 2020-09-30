using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Ctf4e.LabServer.Options
{
    public class LabOptions
    {
        public string CtfServerBaseUrl { get; set; }
        
        public bool DevelopmentMode { get; set; }
        
        public bool PassAsGroup { get; set; }

        public string PageTitle { get; set; }
        
        public string NavbarTitle { get; set; }
        
        public string UserStateDirectory { get; set; }
        
        public LabOptionsExerciseEntry[] Exercises { get; set; }
    }

    public class LabOptionsExerciseEntry
    {
        /// <summary>
        /// ID of this exercise. This is used to keep track of already solved exercises, and thus should not be changed during runtime.
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// Used to identify the lab's exercises in the CTF system. Can be null.
        /// </summary>
        public int? CtfExerciseNumber { get; set; }

        /// <summary>
        /// Optional. Flag code to show when this exercise is solved. Needs to be present when <see cref="CtfExerciseNumber"/> is null.
        /// </summary>
        public string FlagCode { get; set; }
        
        /// <summary>
        /// Exercise title.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Optional. Format string for the exercise description. {0} can be used as a placeholder for a solution name.
        /// This field allows HTML.
        /// </summary>
        public string DescriptionFormat { get; set; }
        
        /// <summary>
        /// Optional. URL which needs to visited for solving this exercise.
        /// </summary>
        public string Link { get; set; }
        
        /// <summary>
        /// Controls whether the user is allowed to enter any of the valid solutions to pass (true),
        /// or whether only a certain, randomly picked solution is allowed.
        /// </summary>
        public bool AllowAnySolution { get; set; }

        public LabOptionsSolutionEntry[] ValidSolutions { get; set; }
    }

    public class LabOptionsSolutionEntry
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }
}

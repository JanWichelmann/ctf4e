using System;
using System.ComponentModel.DataAnnotations;

namespace Ctf4e.Server.InputModels;

public class AdminExerciseSubmissionInputModel
{
    [Required]
    public int ExerciseId { get; set; }
    
    public int? UserId { get; set; }
    public int? GroupId {get; set; }
    
    public DateTime? SubmissionTime { get; set; }
    
    [Required]
    public bool ExercisePassed { get; set; }

    [Range(1, int.MaxValue)]
    public int Weight { get; set; } = 1;
}
using System;
using System.ComponentModel.DataAnnotations;

namespace Ctf4e.Server.Models;

public class ExerciseSubmission
{
    public int Id { get; set; }

    [Required]
    public int ExerciseId { get; set; }

    public Exercise Exercise { get; set; }

    [Required]
    public int UserId { get; set; }

    public User User { get; set; }

    public DateTime SubmissionTime { get; set; }

    public bool ExercisePassed { get; set; }

    /// <summary>
    ///     Factor for penalty points. Always 1 for successful tries.
    /// </summary>
    public int Weight { get; set; }
}
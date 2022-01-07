using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ctf4e.Server.Data.Entities;

public class UserEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string DisplayName { get; set; }

    public int MoodleUserId { get; set; }

    [Required]
    [StringLength(100)]
    public string MoodleName { get; set; }

    public bool IsAdmin { get; set; }

    public bool IsTutor { get; set; }

    public string GroupFindingCode { get; set; }

    public ICollection<FlagSubmissionEntity> FlagSubmissions { get; set; }

    public ICollection<ExerciseSubmissionEntity> ExerciseSubmissions { get; set; }

    public int? GroupId { get; set; }
    public GroupEntity Group { get; set; }
}
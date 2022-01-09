using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Ctf4e.Server.Authorization;

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
        
    [Required]
    public UserPrivileges Privileges { get; set; }
    
    [Required(AllowEmptyStrings = true)]
    [StringLength(200)]
    public string PasswordHash { get; set; }
    
    public bool IsTutor { get; set; }

    [Required]
    [StringLength(100)]
    public string GroupFindingCode { get; set; }

    public ICollection<FlagSubmissionEntity> FlagSubmissions { get; set; }

    public ICollection<ExerciseSubmissionEntity> ExerciseSubmissions { get; set; }

    public int? GroupId { get; set; }
    public GroupEntity Group { get; set; }
}
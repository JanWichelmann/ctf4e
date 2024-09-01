using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Ctf4e.Utilities;

namespace Ctf4e.Server.InputModels;

public class AdminFlagSubmissionInputModel
{
    [Required]
    public int FlagId { get; set; }
    
    [Required]
    public int UserId { get; set; }
    
    [JsonConverter(typeof(CustomDateTimeJsonConverter))]
    public DateTime? SubmissionTime { get; set; }
}
using System.ComponentModel.DataAnnotations;

namespace Ctf4e.LabServer.InputModels;

public class AdminConfigurationInputModel
{
    [Required]
    public string Configuration { get; set; }
}
using System.ComponentModel.DataAnnotations;

namespace Ctf4e.LabServer.InputModels;

public class AdminConfigurationInputModel
{
    [Required]
    public string FileToChange { get; set; }
    
    [Required]
    public string Configuration { get; set; }
    
    public bool Writable { get; set; }
}
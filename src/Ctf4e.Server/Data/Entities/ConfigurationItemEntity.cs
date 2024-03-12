using System.ComponentModel.DataAnnotations;

namespace Ctf4e.Server.Data.Entities;

public class ConfigurationItemEntity
{
    [Key]
    [StringLength(255)]
    public string Key { get; set; }

    [StringLength(5000)]
    public string Value { get; set; }
}
using System.ComponentModel.DataAnnotations;

namespace Auth.Domain.Models;

public class Permission
{
    [Key]
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public DateTime CreateAt { get; set; }
    public DateTime UpdateAt { get; set; }
}
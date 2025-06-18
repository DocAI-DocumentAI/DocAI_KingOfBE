using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Auth.Domain.Models;

public class UserDepartment
{
    [Key]
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    [ForeignKey("UserId")]
    public User User { get; set; }
    public Guid DepartmentId { get; set; }
    [ForeignKey("DepartmentId")]
    public Department Department { get; set; }
    public bool IsDepartmentHead {get; set;}
}
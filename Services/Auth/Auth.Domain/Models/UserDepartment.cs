using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Auth.Domain.Models;

public class UserDepartment
{
    [Key]
    public Guid Id { get; set; }
    public Guid userId { get; set; }
    [ForeignKey("userId")]
    public User User { get; set; }
    public Guid departmentId { get; set; }
    [ForeignKey("departmentId")]
    public Department Department { get; set; }
    public bool IsDepartmentHead {get; set;}
}
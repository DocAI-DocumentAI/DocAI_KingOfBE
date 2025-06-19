using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Auth.Domain.Models;

public class ActiveKey
{
    [Key]
    public Guid Id { get; set; }
    public string ActivationCode  { get; set; }
    public string RoleName { get; set; }

    // THÊM: Liên kết ActiveKey với một phòng ban
    public Guid? DepartmentId { get; set; } // Có thể null nếu ActiveKey dành cho roles global (như Admin)
    [ForeignKey("DepartmentId")]
    public virtual Department? Department { get; set; } // Navigation property
}
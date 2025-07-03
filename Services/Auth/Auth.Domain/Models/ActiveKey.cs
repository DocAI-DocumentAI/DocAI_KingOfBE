// using System.ComponentModel.DataAnnotations;
// using System.ComponentModel.DataAnnotations.Schema;
// using Auth.Domain.Models;
//
// public class ActiveKey
// {
//     [Key]
//     public Guid Id { get; set; }
//
//     public string ActivationCode { get; set; }
//
//     public Guid? UsedByUserId { get; set; }
//     [ForeignKey("UsedByUserId")]
//     public virtual User? UsedByUser { get; set; }
//
//     public Guid CreatedByUserId { get; set; }
//     [ForeignKey("CreatedByUserId")]
//     public virtual User CreatedByUser { get; set; }
//
//     public string Status { get; set; }
//
//     public Guid RoleId { get; set; }
//     [ForeignKey("RoleId")]
//     public virtual Role Role { get; set; }
//
//     public Guid DepartmentId { get; set; }
//     [ForeignKey("DepartmentId")]
//     public virtual Department Department { get; set; }
//
//     public DateTime CreatedAt { get; set; }
//     public DateTime UpdatedAt { get; set; }
// }
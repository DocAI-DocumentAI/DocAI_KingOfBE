using System.ComponentModel.DataAnnotations;

namespace Auth.API.Payload.Request.ActiveKey
{
    public class UpdateActiveKeyRequest
    {
        [Required(ErrorMessage = "Status is required")]
        [StringLength(10, ErrorMessage = "Status must not exceed 10 characters")]
        public string Status { get; set; }

        [Required(ErrorMessage = "RoleId is required")]
        public Guid RoleId { get; set; }

        [Required(ErrorMessage = "DepartmentId is required")]
        public Guid DepartmentId { get; set; }
    }
}

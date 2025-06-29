using System.ComponentModel.DataAnnotations;

namespace Auth.API.Payload.Request.User
{
    public class ChangeDepartmentRequest
    {
        [Required(ErrorMessage = "UserId is required")]
        public Guid UserId { get; set; }
        
        [Required(ErrorMessage = "DepartmentId is required")]
        public Guid DepartmentId { get; set; }
    }
}

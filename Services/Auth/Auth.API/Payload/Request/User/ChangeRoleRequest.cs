using System.ComponentModel.DataAnnotations;

namespace Auth.API.Payload.Request
{
    public class ChangeRoleRequest
    {
        [Required]
        public string ActivationCode { get; set; }
    }
}
using System.ComponentModel.DataAnnotations;

namespace ChatBox.API.Payload.Request.DocumentClientService
{
    public class DocumentAccessRequest
    {
        [Required]
        public string DocumentId { get; set; }

        [Required]
        public Guid UserId { get; set; }

        public string AccessType { get; set; } = "read"; // read, write, share, admin
        public bool CheckInheritedPermissions { get; set; } = true;
        public DateTime? EffectiveDate { get; set; }
    }
}

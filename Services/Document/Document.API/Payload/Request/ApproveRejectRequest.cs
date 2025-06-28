using System.ComponentModel.DataAnnotations;

namespace Document.API.Payload.Request
{
    public class ApproveRejectRequest
    {
        [Required]
        public bool Approved { get; set; }

        /// <summary>
        /// Optional comments for an approval, but required for a rejection.
        /// This provides feedback to the document creator.
        /// </summary>
        public string? Comments { get; set; }
    }
}

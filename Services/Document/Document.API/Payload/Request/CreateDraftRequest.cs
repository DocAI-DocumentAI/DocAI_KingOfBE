using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Document.API.Payload.Request
{
    public class CreateDraftRequest
    {
        [Required]
        public string Title { get; set; }
        public string VersionName { get; set; }
        public string? Summary { get; set; }
        public string? SignedBy { get; set; }
        public string? Description { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveUntil { get; set; }
        public List<string>? Tags { get; set; }
        public string DepartmentId { get; set; }
        public IFormFile File { get; set; }
        public string? ReplacementDocumentId { get; set; }

        [Required(ErrorMessage = "Document type is required")]
        public string DocumentTypeId { get; set; }
    }
}

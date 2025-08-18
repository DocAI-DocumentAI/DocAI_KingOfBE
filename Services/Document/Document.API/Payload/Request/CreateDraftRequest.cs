using Microsoft.AspNetCore.Mvc;

namespace Document.API.Payload.Request
{
    public class CreateDraftRequest
    {
        public string Title { get; set; }
        public string VersionName { get; set; }
        public string? Summary { get; set; }
        public string? SignedBy { get; set; }
        public string? Description { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveUntil { get; set; }
        public List<string>? Tags { get; set; }
        public IFormFile File { get; set; }
        public string? ReplacementDocumentId { get; set; }

        public string DocumentTypeId { get; set; }

        /// <summary>
        /// Indicates whether the document is public (accessible to all employees) or private (restricted to same department)
        /// </summary>
        public bool IsPublic { get; set; } = false;

        /// <summary>
        /// Target functional folder ID where the document should be moved when approved
        /// Documents are initially created in drafts folder and moved to this folder upon approval
        /// If not specified, document will remain in drafts folder after approval (not recommended)
        /// </summary>
        public string? FolderId { get; set; }
    }
}

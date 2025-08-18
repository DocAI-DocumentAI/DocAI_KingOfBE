using Microsoft.AspNetCore.Mvc;

namespace Document.API.Payload.Request
{
    public class CreateNewVersionDraftRequest
    {
        public string Title { get; set; }

        public string VersionName { get; set; }

        public string? Summary { get; set; }

        public string? SignedBy { get; set; }

        public DateTime? EffectiveFrom { get; set; }

        public DateTime? EffectiveUntil { get; set; }

        public List<string>? Tags { get; set; }

        public IFormFile File { get; set; }

        /// <summary>
        /// Indicates whether the document is public (accessible to all employees) or private (restricted to same department)
        /// </summary>
        public bool IsPublic { get; set; } = false;

        /// <summary>
        /// Target folder ID where the new version should be uploaded
        /// If not specified, new version will be uploaded to the same folder as the original document
        /// </summary>
        public string? FolderId { get; set; }
    }
}

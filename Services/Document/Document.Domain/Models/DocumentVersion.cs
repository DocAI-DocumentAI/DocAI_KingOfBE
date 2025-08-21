using Document.Domain.Enums;
using Document.Domain.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Document.Domain.Models
{
    public class DocumentVersion : BaseEntity
    {
        public DocumentVersion()
        {
            DocumentTags = new HashSet<DocumentTag>();
            ApprovalLogs = new HashSet<ApprovalLog>();
        }
        public string VersionName { get; set; }
        public string Title { get; set; }
        public string? Summary { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string FileType { get; set; }
        public long FileSize { get; set; }
        public string FileHash { get; set; }
        public string? GoogleDriveFileId { get; set; }
        public int? TotalDownloads { get; set; } = 0;
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveUntil { get; set; }
        public string? SignedBy { get; set; }
        public StatusEnum Status { get; set; }
        public bool IsOfficial { get; set; }
        public bool IsPublic { get; set; }
        public DateTime? LastSubmitted { get; set; }
        public string? SubmittedBy { get; set; }
        public string DocumentFileId { get; set; }

        /// <summary>
        /// Folder that contains this document version
        /// Required for new folder-based organization
        /// </summary>
        public string? FolderId { get; set; }

        /// <summary>
        /// Target folder where the document should be moved when approved
        /// Used during the approval workflow to determine final destination
        /// </summary>
        public string? TargetFolderId { get; set; }

        // Navigation Properties
        public DocumentFile DocumentFile { get; set; }

        /// <summary>
        /// Folder that contains this document version
        /// </summary>
        [ForeignKey(nameof(FolderId))]
        public virtual Folder? Folder { get; set; }

        /// <summary>
        /// Target folder for approval workflow
        /// </summary>
        [ForeignKey(nameof(TargetFolderId))]
        public virtual Folder? TargetFolder { get; set; }

        public virtual ICollection<DocumentTag> DocumentTags { get; set; }
        public virtual ApprovalClaim? ApprovalClaim { get; set; }
        public virtual ICollection<ApprovalLog> ApprovalLogs { get; set; }
    }
}

using System.Text;

namespace Document.API.Payload.Response
{
    public class DocumentSourceResponse
    {
        public string DocumentId { get; set; }
        public string VersionId { get; set; }
        public string DepartmentId { get; set; }
        public string OwnerId { get; set; }
        public string Status { get; set; }
        public string VersionName { get; set; }
        public bool IsOfficial { get; set; }
        public bool IsPublic { get; set; }
        public DateTime? ApprovalDate { get; set; }
        public string CreatedBy { get; set; }
        public string SubmittedBy { get; set; }
        public DateTime? LastSubmitted { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Summary { get; set; }
        public string VersionTitle { get; set; }
        public string DocumentType { get; set; }
        public string DocumentTypeName { get; set; }
        public string DocumentTypeDescription { get; set; }
        public string SignedBy { get; set; } 
        public DateTime? EffectiveFrom { get; set; } 
        public DateTime? EffectiveUntil { get; set; } 
        public List<string> Tags { get; set; } = new();
        public string DepartmentName { get; set; }
        public string OwnerName { get; set; }
        public string OwnerEmail { get; set; }
        public string ReviewerId { get; set; }
        public string ReviewerName { get; set; } 
        public string ReviewComments { get; set; }
        public string ReviewAction { get; set; }
        public DateTime? ReviewDate { get; set; }
        public string FileName { get; set; } 
        public string FileType { get; set; } 
        public long? FileSize { get; set; } 
        public string FileHash { get; set; }
        public string GoogleDriveFileId { get; set; }
        public string FolderPath { get; set; }
        public string StorageLocation { get; set; }
        public string ReplacementOfDocumentId { get; set; } 
        public string ReplacedDocumentId { get; set; } 
        public string PreviousApprovedVersionId { get; set; }
        public string PreviousApprovedVersionName { get; set; }
        public DateTime? PreviousApprovedAt { get; set; }
        public string Visibility { get; set; }
        public string PermissionLevel { get; set; }
        public string DepartmentRestriction { get; set; }
        public double RelevanceScore { get; set; }
        public string SearchSnippet { get; set; } 
        public List<string> MatchedKeywords { get; set; } = new();
        public string ContentPreview { get; set; } 

        public string ApprovedBy { get; set; } 
        public DateTime? SignedDate { get; set; } 
        public string Category { get; set; } 
        public string Priority { get; set; } 
        public bool IsLatestVersion { get; set; } 
        public int VersionNumber { get; set; } 
        public string ParentDocumentId { get; set; }
        public List<string> RelatedDocumentIds { get; set; } = new(); 
        public string DocumentLanguage { get; set; } 
        public int PageCount { get; set; } 
        public int WordCount { get; set; } 
        public string AccessLevel { get; set; } 
        public bool IsArchived { get; set; } 
        public DateTime? ExpiryDate { get; set; }
        public string ConfidentialityLevel { get; set; } 

        // ✅ EXISTING COMPUTED PROPERTIES - GIỮ NGUYÊN
        public bool IsExpired => EffectiveUntil.HasValue && EffectiveUntil.Value < DateTime.UtcNow;
        public bool IsActive => EffectiveFrom.HasValue && EffectiveFrom.Value <= DateTime.UtcNow && !IsExpired;
        public string FormattedFileSize => FormatFileSize(FileSize);
        public string EffectivePeriod => FormatEffectivePeriod(EffectiveFrom, EffectiveUntil);
        public string DocumentAge => FormatDocumentAge(ApprovalDate ?? LastSubmitted);
        public string CurrentStatus => GetCurrentStatus();
        public string FullDocumentInfo => GetFullDocumentInfo();

        // ✅ EXISTING HELPER METHODS - GIỮ NGUYÊN
        private string FormatFileSize(long? sizeInBytes)
        {
            if (!sizeInBytes.HasValue) return "Unknown";
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = sizeInBytes.Value;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        private string FormatEffectivePeriod(DateTime? from, DateTime? until)
        {
            if (!from.HasValue && !until.HasValue) return "Không giới hạn thời gian";
            if (!from.HasValue) return $"Đến {until:dd/MM/yyyy}";
            if (!until.HasValue) return $"Từ {from:dd/MM/yyyy}";
            return $"{from:dd/MM/yyyy} - {until:dd/MM/yyyy}";
        }

        private string FormatDocumentAge(DateTime? createdDate)
        {
            if (!createdDate.HasValue) return "Unknown";
            var age = DateTime.UtcNow - createdDate.Value;

            if (age.TotalDays < 1) return "Hôm nay";
            if (age.TotalDays < 7) return $"{(int)age.TotalDays} ngày trước";
            if (age.TotalDays < 30) return $"{(int)(age.TotalDays / 7)} tuần trước";
            if (age.TotalDays < 365) return $"{(int)(age.TotalDays / 30)} tháng trước";
            return $"{(int)(age.TotalDays / 365)} năm trước";
        }

        private string GetCurrentStatus()
        {
            if (IsExpired) return "Đã hết hiệu lực";
            if (IsActive) return "Đang có hiệu lực";
            if (EffectiveFrom.HasValue && EffectiveFrom.Value > DateTime.UtcNow)
                return "Chưa có hiệu lực";
            if (Status?.Equals("approved", StringComparison.OrdinalIgnoreCase) == true)
                return "Đã phê duyệt";
            return Status ?? "Unknown";
        }

        private string GetFullDocumentInfo()
        {
            var info = new List<string>
            {
                $"Title: {Title ?? "Unknown"}"
            };

            if (!string.IsNullOrEmpty(SignedBy))
                info.Add($"Signed by: {SignedBy}");
            if (!string.IsNullOrEmpty(DepartmentName))
                info.Add($"Department: {DepartmentName}");
            if (!string.IsNullOrEmpty(OwnerName))
                info.Add($"Created by: {OwnerName}");

            info.Add($"Status: {CurrentStatus}");
            info.Add($"Effective period: {EffectivePeriod}");

            if (!string.IsNullOrEmpty(FileType))
                info.Add($"File type: {FileType}");
            if (FileSize.HasValue)
                info.Add($"File size: {FormattedFileSize}");

            info.Add($"Document age: {DocumentAge}");
            info.Add($"Official: {(IsOfficial ? "Yes" : "No")}");
            info.Add($"Public: {(IsPublic ? "Yes" : "No")}");

            if (Tags?.Any() == true)
                info.Add($"Tags: {string.Join(", ", Tags)}");

            return string.Join(" | ", info);
        }

        public Dictionary<string, object> GetAllMetadata()
        {
            return new Dictionary<string, object>
            {
                ["documentId"] = DocumentId,
                ["title"] = Title,
                ["signedBy"] = SignedBy,
                ["effectiveFrom"] = EffectiveFrom,
                ["effectiveUntil"] = EffectiveUntil,
                ["currentStatus"] = CurrentStatus,
                ["departmentName"] = DepartmentName,
                ["ownerName"] = OwnerName,
                ["reviewerName"] = ReviewerName,
                ["fileName"] = FileName,
                ["fileType"] = FileType,
                ["formattedFileSize"] = FormattedFileSize,
                ["tags"] = Tags,
                ["isOfficial"] = IsOfficial,
                ["isPublic"] = IsPublic,
                ["isActive"] = IsActive,
                ["isExpired"] = IsExpired,
                ["documentAge"] = DocumentAge,
                ["effectivePeriod"] = EffectivePeriod,
                ["replacementOfDocumentId"] = ReplacementOfDocumentId,
                ["replacedDocumentId"] = ReplacedDocumentId,
                // ✅ NEW metadata fields
                ["approvedBy"] = ApprovedBy,
                ["signedDate"] = SignedDate,
                ["category"] = Category,
                ["priority"] = Priority,
                ["isLatestVersion"] = IsLatestVersion,
                ["versionNumber"] = VersionNumber,
                ["parentDocumentId"] = ParentDocumentId,
                ["relatedDocumentIds"] = RelatedDocumentIds,
                ["documentLanguage"] = DocumentLanguage,
                ["pageCount"] = PageCount,
                ["wordCount"] = WordCount,
                ["accessLevel"] = AccessLevel,
                ["isArchived"] = IsArchived,
                ["confidentialityLevel"] = ConfidentialityLevel
            };
        }

        public string ToAIContextString()
        {
            var context = new StringBuilder();

            context.AppendLine($"📄 Document: {Title}");
            if (!string.IsNullOrEmpty(SignedBy))
                context.AppendLine($"👤 Signed by: {SignedBy}");
            if (!string.IsNullOrEmpty(ApprovedBy))
                context.AppendLine($"✅ Approved by: {ApprovedBy}");
            context.AppendLine($"📅 Status: {CurrentStatus}");
            context.AppendLine($"⏰ Effective: {EffectivePeriod}");
            if (!string.IsNullOrEmpty(DepartmentName))
                context.AppendLine($"🏢 Department: {DepartmentName}");
            if (!string.IsNullOrEmpty(OwnerName))
                context.AppendLine($"👨‍💼 Owner: {OwnerName}");
            context.AppendLine($"📁 File: {FileType} ({FormattedFileSize})");
            context.AppendLine($"🔒 Access: {(IsPublic ? "Public" : "Restricted")}");
            if (!string.IsNullOrEmpty(Category))
                context.AppendLine($"📂 Category: {Category}");
            if (!string.IsNullOrEmpty(Priority))
                context.AppendLine($"⚡ Priority: {Priority}");
            if (Tags?.Any() == true)
                context.AppendLine($"🏷️ Tags: {string.Join(", ", Tags)}");

            return context.ToString();
        }
    }
}

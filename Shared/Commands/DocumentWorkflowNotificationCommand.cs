namespace Shared.Commands
{
    /// <summary>
    /// Base class for document workflow notification commands
    /// </summary>
    public abstract class DocumentWorkflowNotificationCommand
    {
        public string DocumentId { get; set; } = string.Empty;
        public string DocumentTitle { get; set; } = string.Empty;
        public string DocumentVersion { get; set; } = string.Empty;
        public string? DocumentLink { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Command to send document submission notification to department managers
    /// </summary>
    public class DocumentSubmissionNotificationCommand : DocumentWorkflowNotificationCommand
    {
        public Guid SubmitterId { get; set; } = Guid.Empty;
        public string SubmitterEmail { get; set; } = string.Empty;
        public string SubmitterName { get; set; } = string.Empty;
        public Guid DepartmentId { get; set; } = Guid.Empty;
        public string DepartmentName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Command to send document submission confirmation to the submitter
    /// </summary>
    public class DocumentSubmissionConfirmationCommand : DocumentWorkflowNotificationCommand
    {
        public string SubmitterEmail { get; set; } = string.Empty;
        public string SubmitterName { get; set; } = string.Empty;
        public Guid SubmitterId { get; set; } = Guid.Empty;
        public Guid DepartmentId { get; set; } = Guid.Empty;
        public string DepartmentName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Command to send document approval notification to document owner
    /// </summary>
    public class DocumentApprovalNotificationCommand : DocumentWorkflowNotificationCommand
    {
        public string OwnerEmail { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public Guid ApproverId { get; set; } = Guid.Empty;
        public string ApproverEmail { get; set; } = string.Empty;
        public string ApproverName { get; set; } = string.Empty;
        public string? Comments { get; set; }
    }

    /// <summary>
    /// Command to send document rejection notification to document owner
    /// </summary>
    public class DocumentRejectionNotificationCommand : DocumentWorkflowNotificationCommand
    {
        public string OwnerEmail { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public Guid ReviewerId { get; set; } = Guid.Empty;
        public string ReviewerEmail { get; set; } = string.Empty;
        public string ReviewerName { get; set; } = string.Empty;
        public string RejectionComments { get; set; } = string.Empty;
    }

    /// <summary>
    /// Command to send document publication notification to all department users
    /// </summary>
    public class DocumentPublicationNotificationCommand : DocumentWorkflowNotificationCommand
    {
        public string DepartmentId { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public Guid ApproverId { get; set; } = Guid.Empty;
        public string ApproverEmail { get; set; } = string.Empty;
        public string ApproverName { get; set; } = string.Empty;
        public bool IsPublic { get; set; }
        public string DocumentTypeId { get; set; } = string.Empty;
        public string DocumentTypeName { get; set; } = string.Empty;
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveUntil { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
    }
}

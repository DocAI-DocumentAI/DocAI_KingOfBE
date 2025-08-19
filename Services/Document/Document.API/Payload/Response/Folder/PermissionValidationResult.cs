using Document.API.Services.Interfaces;

namespace Document.API.Payload.Response.Folder
{
    /// <summary>
    /// Result of permission validation for a specific action
    /// </summary>
    public class PermissionValidationResult
    {
        /// <summary>
        /// Whether the action is allowed
        /// </summary>
        public bool IsAllowed { get; set; }

        /// <summary>
        /// Reason for denial if action is not allowed
        /// </summary>
        public string? DenialReason { get; set; }

        /// <summary>
        /// Required permission level for the action
        /// </summary>
        public string RequiredPermission { get; set; }

        /// <summary>
        /// User's actual permission level
        /// </summary>
        public string? UserPermission { get; set; }

        /// <summary>
        /// Action that was validated
        /// </summary>
        public FolderAction Action { get; set; }

        /// <summary>
        /// Additional context or suggestions
        /// </summary>
        public List<string> Suggestions { get; set; } = new List<string>();

        /// <summary>
        /// Whether the user can request elevated permissions
        /// </summary>
        public bool CanRequestElevation { get; set; }

        /// <summary>
        /// Who can grant the required permission
        /// </summary>
        public List<string> PermissionGranters { get; set; } = new List<string>();
    }

    /// <summary>
    /// Response for permission audit trail
    /// </summary>
    public class FolderPermissionAuditResponse
    {
        /// <summary>
        /// Audit entry ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Folder ID
        /// </summary>
        public string FolderId { get; set; }

        /// <summary>
        /// Folder name at time of change
        /// </summary>
        public string FolderName { get; set; }

        /// <summary>
        /// Type of change (Created, Updated, Deleted, Inherited)
        /// </summary>
        public string ChangeType { get; set; }

        /// <summary>
        /// User or department affected
        /// </summary>
        public string TargetId { get; set; }

        /// <summary>
        /// Target type (User, Department)
        /// </summary>
        public string TargetType { get; set; }

        /// <summary>
        /// Target name for display
        /// </summary>
        public string TargetName { get; set; }

        /// <summary>
        /// Previous permission (for updates)
        /// </summary>
        public string? PreviousPermission { get; set; }

        /// <summary>
        /// New permission
        /// </summary>
        public string NewPermission { get; set; }

        /// <summary>
        /// Whether permission was denied
        /// </summary>
        public bool IsDenied { get; set; }

        /// <summary>
        /// When the change occurred
        /// </summary>
        public DateTime ChangeTime { get; set; }

        /// <summary>
        /// Who made the change
        /// </summary>
        public string ChangedBy { get; set; }

        /// <summary>
        /// Reason for the change
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Source of the change (Direct, Inherited, Bulk, System)
        /// </summary>
        public string ChangeSource { get; set; }
    }

    /// <summary>
    /// Response for permission conflicts
    /// </summary>
    public class PermissionConflictResponse
    {
        /// <summary>
        /// Conflict ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Folder ID where conflict exists
        /// </summary>
        public string FolderId { get; set; }

        /// <summary>
        /// Folder name
        /// </summary>
        public string FolderName { get; set; }

        /// <summary>
        /// User or department with conflicting permissions
        /// </summary>
        public string TargetId { get; set; }

        /// <summary>
        /// Target type (User, Department)
        /// </summary>
        public string TargetType { get; set; }

        /// <summary>
        /// Target name for display
        /// </summary>
        public string TargetName { get; set; }

        /// <summary>
        /// Conflicting permissions
        /// </summary>
        public List<ConflictingPermission> ConflictingPermissions { get; set; } = new List<ConflictingPermission>();

        /// <summary>
        /// Type of conflict (AllowDeny, Duplicate, Inheritance)
        /// </summary>
        public string ConflictType { get; set; }

        /// <summary>
        /// Severity of the conflict (Low, Medium, High)
        /// </summary>
        public string Severity { get; set; }

        /// <summary>
        /// Suggested resolution
        /// </summary>
        public string SuggestedResolution { get; set; }

        /// <summary>
        /// When the conflict was detected
        /// </summary>
        public DateTime DetectedAt { get; set; }
    }

    /// <summary>
    /// Details of a conflicting permission
    /// </summary>
    public class ConflictingPermission
    {
        /// <summary>
        /// Permission ID
        /// </summary>
        public string PermissionId { get; set; }

        /// <summary>
        /// Permission type
        /// </summary>
        public string PermissionType { get; set; }

        /// <summary>
        /// Whether permission is denied
        /// </summary>
        public bool IsDenied { get; set; }

        /// <summary>
        /// Source of permission (Direct, Inherited, Department)
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// When permission was created
        /// </summary>
        public DateTime CreatedTime { get; set; }

        /// <summary>
        /// Who created the permission
        /// </summary>
        public string CreatedBy { get; set; }
    }

    /// <summary>
    /// Result of conflict resolution
    /// </summary>
    public class PermissionConflictResolutionResult
    {
        /// <summary>
        /// Whether resolution was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Resolution strategy used
        /// </summary>
        public string Strategy { get; set; }

        /// <summary>
        /// Number of permissions modified
        /// </summary>
        public int PermissionsModified { get; set; }

        /// <summary>
        /// Final effective permission after resolution
        /// </summary>
        public string? FinalPermission { get; set; }

        /// <summary>
        /// Details of what was changed
        /// </summary>
        public List<string> ChangeDetails { get; set; } = new List<string>();

        /// <summary>
        /// Any errors that occurred during resolution
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();
    }
}

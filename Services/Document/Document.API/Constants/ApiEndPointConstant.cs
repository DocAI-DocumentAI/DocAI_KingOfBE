namespace Document.API.Constants;

public class ApiEndPointConstant
{
    static ApiEndPointConstant() {}

    public const string RootEndPoint = "/api";
    public const string DocumentApiVersion = "/document"; // New constant
    public const string ApiEndpoint = RootEndPoint + DocumentApiVersion; // Base for document-related APIs

    public class Document
    {
        // Changed to use ApiEndpoint directly, and resource names are more specific
        public const string UploadDraft = ApiEndpoint + "/drafts"; // Changed from /documents
        public const string GetDocumentDraft = ApiEndpoint + "/drafts/{id}"; // Changed from /documents/{id}
        public const string EditDraft = ApiEndpoint + "/drafts/{id}"; // Changed from /documents/{id}
        public const string DeleteDraft = ApiEndpoint + "/drafts/{id}"; // Changed from /documents/{id}
        public const string DeleteApprovedDocument = ApiEndpoint + "/documents/{id}/delete"; // Delete approved/archived documents
        public const string DeleteDocumentVersion = ApiEndpoint + "/documents/{documentId}/versions/{versionId}/delete"; // Delete specific version
        public const string GetOfficialDocument = ApiEndpoint + "/documents/{id}"; // Changed from /official-documents/{id}
        public const string GetAllOfficialDocuments = ApiEndpoint + "/documents"; // Changed from /official-documents
        public const string GetMyDocuments = ApiEndpoint + "/my-documents"; // Remains the same
        public const string CreateNewVersion = ApiEndpoint + "/documents/{id}/versions"; // Remains the same
        public const string AnalyzeDocument = ApiEndpoint + "/analyze-document"; // Remains the same
        public const string RegenerateSummary = ApiEndpoint + "/regenerate-summary"; // New endpoint for enhanced summary regeneration during document creation
        public const string SemanticSearch = ApiEndpoint + "/semantic-search"; // Remains the same
        public const string EnhancedSemanticSearch = ApiEndpoint + "/enhanced-semantic-search"; // AI-powered conversational search
        public const string FullTextSearch = ApiEndpoint + "/full-text-search";
        public const string GetDrafts = ApiEndpoint + "/drafts";
        public const string GetDraftById = ApiEndpoint + "/drafts/{id}";
        public const string GetRejectedDocuments = ApiEndpoint + "/rejected-documents";
        public const string GetRejectedById = ApiEndpoint + "/rejected-documents/{id}";
        public const string GetMyDocumentDetail = ApiEndpoint + "/my-documents/{id}";


        // My documents extras
        public const string GetMyDocumentsWithStats = GetMyDocuments + "/with-stats";
        public const string GetEditorApprovalHistory = GetMyDocuments + "/approval-history";
        public const string GetEditorApprovalHistoryDetail = GetMyDocuments + "/approval-history/{id}";

        // File serving endpoints
        public const string ViewFile = ApiEndpoint + "/files/{versionId}/view";
        public const string DownloadFile = ApiEndpoint + "/files/{versionId}/download";
        public const string GetFileInfo = ApiEndpoint + "/files/{versionId}/info";

        // Iframe viewing endpoints
        public const string GetIframeViewingUrl = ApiEndpoint + "/files/{versionId}/iframe-url";
        public const string GetSharingLink = ApiEndpoint + "/files/{versionId}/sharing-link";
        public const string ValidateFileAccess = ApiEndpoint + "/files/{versionId}/validate-access";

        // Recommendation endpoint
        public const string GetRecommendations = ApiEndpoint + "/documents/{documentId}/recommendations";

        // Replacement suggestion endpoints
        public const string GetReplacementSuggestions = ApiEndpoint + "/replacement-suggestions";
        public const string GetReplacementSuggestionsForEdit = ApiEndpoint + "/documents/{documentId}/replacement-suggestions";
        public const string GetReplacementScoringBreakdown = ApiEndpoint + "/replacement-suggestions/{candidateId}/scoring";
        public const string ValidateReplacement = ApiEndpoint + "/documents/{documentId}/can-replace";
        public const string GetReplaceableDocuments = ApiEndpoint + "/replaceable-documents";
    }

    public class Approval
    {
        public const string Submit = ApiEndpoint + "/submit/{id}";
        public const string ApproveOrReject = ApiEndpoint + "/review/{id}";
        public const string GetApprovalQueue = ApiEndpoint + "/approval-queue";
        public const string Claim = ApiEndpoint + "/claim/{id}";
        public const string ReleaseClaim = ApiEndpoint + "/release-claim/{id}";
        public const string KeepClaimAlive = ApiEndpoint + "/keep-claim-alive/{id}";
        public const string GetApprovalQueueDetail = ApiEndpoint + "/approval-queue/detail/{id}";
        public const string ArchiveDocument = ApiEndpoint + "/archive/{id}";
        public const string DeleteArchivedDocument = ApiEndpoint + "/archived/{id}/delete";
        public const string DeleteEntireDocument = ApiEndpoint + "/documents/{id}/delete-entire";
    }

    public class Bookmark
    {
        public const string AddBookmark = ApiEndpoint + "/bookmarks/{documentId}";
        public const string RemoveBookmark = ApiEndpoint + "/bookmarks/{documentId}";
        public const string GetBookmarks = ApiEndpoint + "/bookmarks";
    }

    public class DocumentVersion
    {
        public const string GetDocumentVersions = ApiEndpoint + "/documents/{id}/versions";
        public const string GetDocumentVersion = ApiEndpoint + "/documents/{id}/versions/{versionId}";
    }

    public class Tag
    {
        public const string CreateTag = ApiEndpoint + "/tags";
        public const string GetTagById = ApiEndpoint + "/tags/{id}";
        public const string GetTagByName = ApiEndpoint + "/tags/name/{name}";
        public const string GetAllTags = ApiEndpoint + "/tags";
        public const string UpdateTag = ApiEndpoint + "/tags/{id}";
        public const string DeleteTag = ApiEndpoint + "/tags/{id}";
    }

    public class DocumentType
    {
        public const string CreateDocumentType = ApiEndpoint + "/document-types";
        public const string GetDocumentTypeById = ApiEndpoint + "/document-types/{id}";
        public const string GetAllDocumentTypes = ApiEndpoint + "/document-types";
        public const string UpdateDocumentType = ApiEndpoint + "/document-types/{id}";
        public const string DeleteDocumentType = ApiEndpoint + "/document-types/{id}";
        public const string GetDocumentTypesList = ApiEndpoint + "/document-types/list";
    }

    public class GoogleDrive
    {
        public const string CompanyAuth = ApiEndpoint + "/company-auth-url";
        public const string CompanyCallback = ApiEndpoint + "/company-callback";
        public const string Status = ApiEndpoint + "/status";
        public const string TestConnection = ApiEndpoint +"/test-connection";
    }

    public class GoogleDrivePermission
    {
        public const string Base = ApiEndpoint + "/googledrive-permissions";
        public const string SetupDepartment = "setup-department";
        public const string SetupUser = "setup-user";
        public const string BulkInitialize = "bulk-initialize";
        public const string ValidateDepartment = "validate-department/{departmentId}";
    }

    public class Folder
    {
        // Folder CRUD operations
        public const string GetDepartmentTree = ApiEndpoint + "/folders/tree";
        public const string GetPublicTree = ApiEndpoint + "/folders/tree/public";
        public const string GetFolderById = ApiEndpoint + "/folders/{folderId}";
        public const string CreateFolder = ApiEndpoint + "/folders";
        public const string UpdateFolder = ApiEndpoint + "/folders/{folderId}";
        public const string MoveFolder = ApiEndpoint + "/folders/{folderId}/move";
        public const string DeleteFolder = ApiEndpoint + "/folders/{folderId}";

        // Folder navigation and search
        public const string GetAccessibleFolders = ApiEndpoint + "/folders/accessible";
        public const string GetFolderBreadcrumb = ApiEndpoint + "/folders/{folderId}/breadcrumb";
        public const string SearchFolders = ApiEndpoint + "/folders/search";
        public const string ValidateFolderName = ApiEndpoint + "/folders/validate-name";

        // Folder permissions
        public const string GetFolderPermissions = ApiEndpoint + "/folders/{folderId}/permissions";
        public const string SetFolderPermission = ApiEndpoint + "/folders/{folderId}/permissions";
        public const string RemoveFolderPermission = ApiEndpoint + "/folders/{folderId}/permissions/{permissionId}";
        public const string CheckFolderPermission = ApiEndpoint + "/folders/{folderId}/permissions/check";

        // Folder initialization
        public const string InitializeDepartmentFolders = ApiEndpoint + "/folders/initialize/department";
        public const string InitializePublicFolders = ApiEndpoint + "/folders/initialize/public";
    }

    public class FolderDocument
    {
        // Folder document browsing
        public const string BrowseFolderContents = ApiEndpoint + "/folder-documents/browse";
        public const string SearchFolderDocuments = ApiEndpoint + "/folder-documents/search";
        public const string GetFolderDocuments = ApiEndpoint + "/folder-documents/{folderId}/list";
        public const string GetRecentDocuments = ApiEndpoint + "/folder-documents/recent";
        public const string GetFolderDocumentStats = ApiEndpoint + "/folder-documents/{folderId}/statistics";
        public const string MoveDocument = ApiEndpoint + "/folder-documents/{documentVersionId}/move";
        public const string GetDocumentFolderPath = ApiEndpoint + "/folder-documents/{documentVersionId}/folder-path";
        public const string GetDocumentDetail = ApiEndpoint + "/folder-documents/{documentVersionId}/detail";
    }

    public class FolderPermission
    {
        // Basic permission operations
        public const string GetPermissions = ApiEndpoint + "/folder-permissions/{folderId}";
        public const string SetPermission = ApiEndpoint + "/folder-permissions/{folderId}";
        public const string RemovePermission = ApiEndpoint + "/folder-permissions/{folderId}/{permissionId}";
        public const string CheckPermission = ApiEndpoint + "/folder-permissions/{folderId}/check";

        // Folder initialization
        public const string InitializeDepartmentFolders = ApiEndpoint + "/folder-permissions/initialize/department";
        public const string InitializePublicFolders = ApiEndpoint + "/folder-permissions/initialize/public";
    }

    public class FolderPermissionAdvanced
    {
        // Advanced permission analysis
        public const string GetPermissionBreakdown = ApiEndpoint + "/folder-permissions-advanced/{folderId}/breakdown";
        public const string BulkSetPermissions = ApiEndpoint + "/folder-permissions-advanced/{folderId}/bulk";
        public const string InheritPermissions = ApiEndpoint + "/folder-permissions-advanced/{folderId}/inherit";
        public const string PropagatePermissions = ApiEndpoint + "/folder-permissions-advanced/{folderId}/propagate";
        public const string GetUserAccessibleFolders = ApiEndpoint + "/folder-permissions-advanced/accessible";
        public const string ValidateAction = ApiEndpoint + "/folder-permissions-advanced/{folderId}/validate";
    }

    public class FolderAwareApproval
    {
        // Folder-aware approval operations
        public const string SubmitForApproval = ApiEndpoint + "/folder-approval/{versionId}/submit";
        public const string ReviewDocument = ApiEndpoint + "/folder-approval/{versionId}/review";
        public const string GetApprovalQueue = ApiEndpoint + "/folder-approval/queue";
        public const string GetFolderApprovalHistory = ApiEndpoint + "/folder-approval/{folderId}/history";
        public const string GetFolderApprovalStats = ApiEndpoint + "/folder-approval/{folderId}/statistics";
        public const string BulkReviewDocuments = ApiEndpoint + "/folder-approval/bulk-review";
        public const string GetPendingDocumentsInFolder = ApiEndpoint + "/folder-approval/{folderId}/pending";
    }

    public class FolderSync
    {
        public const string VerifySync = ApiEndpoint + "/folder-sync/verify";
        public const string VerifyPermissions = ApiEndpoint + "/folder-sync/verify-permissions/{folderId}";
        public const string SyncPermissions = ApiEndpoint + "/folder-sync/sync-permissions/{folderId}";
    }

    public static class AIConfiguration
    {
        public const string GetAll = ApiEndpoint + "/ai-configurations";
        public const string GetDefault = ApiEndpoint + "/ai-configurations/default";
        public const string GetById = ApiEndpoint + "/ai-configurations/{id}";
        public const string Create = ApiEndpoint + "/ai-configurations";
        public const string Update = ApiEndpoint + "/ai-configurations/{id}";
        public const string Delete = ApiEndpoint + "/ai-configurations/{id}";
        public const string SetDefault = ApiEndpoint + "/ai-configurations/{id}/set-default";
        public const string Initialize = ApiEndpoint + "/ai-configurations/initialize";
    }
}

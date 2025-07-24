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
        public const string GetOfficialDocument = ApiEndpoint + "/documents/{id}"; // Changed from /official-documents/{id}
        public const string GetAllOfficialDocuments = ApiEndpoint + "/documents"; // Changed from /official-documents
        public const string GetMyDocuments = ApiEndpoint + "/my-documents"; // Remains the same
        public const string CreateNewVersion = ApiEndpoint + "/documents/{id}/versions"; // Remains the same
        public const string AnalyzeDocument = ApiEndpoint + "/analyze-document"; // Remains the same
        public const string SemanticSearch = ApiEndpoint + "/semantic-search"; // Remains the same
        public const string FullTextSearch = ApiEndpoint + "/full-text-search";
        public const string GetDrafts = ApiEndpoint + "/drafts";
        public const string GetDraftById = ApiEndpoint + "/drafts/{id}";
        public const string GetRejectedDocuments = ApiEndpoint + "/rejected-documents";
        public const string GetRejectedById = ApiEndpoint + "/rejected-documents/{id}";
        public const string GetMyDocumentDetail = ApiEndpoint + "/my-documents/{id}";

        // File serving endpoints
        public const string ViewFile = ApiEndpoint + "/files/{versionId}/view";
        public const string DownloadFile = ApiEndpoint + "/files/{versionId}/download";
        public const string GetFileInfo = ApiEndpoint + "/files/{versionId}/info";
    }

    public class Approval
    {
        public const string Submit = ApiEndpoint + "/submit/{id}";
        public const string ApproveOrReject = ApiEndpoint + "/review/{id}";
        public const string GetApprovalQueue = ApiEndpoint + "/approval-queue/{departmentId}";
        public const string Claim = ApiEndpoint + "/claim/{id}";
        public const string ReleaseClaim = ApiEndpoint + "/release-claim/{id}";
        public const string GetApprovalQueueDetail = ApiEndpoint + "/approval-queue/detail/{id}";
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
}

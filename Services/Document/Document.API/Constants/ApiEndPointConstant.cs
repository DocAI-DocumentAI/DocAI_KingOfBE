namespace Document.API.Constants;

public class ApiEndPointConstant
{
    static ApiEndPointConstant() {}
    
    public const string RootEndPoint = "/api";
    public const string ApiVersion = "/document";
    public const string ApiEndpoint = RootEndPoint + ApiVersion;

    public class Document
    {
        //public const string GetAll = ApiEndpoint + "/documents";
        //public const string GetById = ApiEndpoint + "/documents/{id}";
        public const string UploadDraft = ApiEndpoint + "/documents";
        public const string GetDocumentDraft = ApiEndpoint + "/documents/{id}";
        public const string EditDraft = ApiEndpoint + "/documents/{id}";
        public const string DeleteDraft = ApiEndpoint + "/documents/{id}";
        public const string GetOfficialDocument = ApiEndpoint + "/official-documents/{id}";
        public const string GetAllOfficialDocuments = ApiEndpoint + "/official-documents";
    }

    public class Approval
    {
        //public const string GetAll = ApiEndpoint + "/documents";
        //public const string GetById = ApiEndpoint + "/documents/{id}";
        public const string Submit = ApiEndpoint + "/submit/{id}";
        public const string ApproveOrReject = ApiEndpoint + "/review/{id}";
        public const string GetApprovalQueue = ApiEndpoint + "/approval-queue/{departmentId}";
    }

    public class Bookmark
    {
        public const string AddBookmark = ApiEndpoint + "/bookmarks/{documentVersionId}";
        public const string RemoveBookmark = ApiEndpoint + "/bookmarks/{documentVersionId}";
        public const string GetBookmarks = ApiEndpoint + "/bookmarks";
    }
}
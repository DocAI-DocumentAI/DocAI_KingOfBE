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
        public const string Submit = ApiEndpoint + "/documents/{id}";
        public const string Approve = ApiEndpoint + "/documents/approve/{id}";
        public const string Upload = ApiEndpoint + "/documents";
        public const string GetDocument = ApiEndpoint + "/documents/{id}";
        public const string UpdateMetaData = ApiEndpoint + "/documents/{id}";
        public const string Delete = ApiEndpoint + "/documents/{id}";
    }
}
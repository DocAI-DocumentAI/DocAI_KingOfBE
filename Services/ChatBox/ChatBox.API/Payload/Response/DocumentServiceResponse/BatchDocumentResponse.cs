namespace ChatBox.API.Payload.Response.DocumentServiceResponse
{
    public class BatchDocumentResponse
    {
        public List<string> AccessibleDocuments { get; set; } = new();
        public List<string> RestrictedDocuments { get; set; } = new();
        public Dictionary<string, string> AccessReasons { get; set; } = new();
    }
}

namespace ChatBox.API.Payload.Request
{
    public class SearchDocumentRequestExternal
    {
        public string Query { get; set; }
        public List<string>? Filters { get; set; }
        public double MinRelevance { get; set; }
    }
}

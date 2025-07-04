namespace ChatBox.API.Payload.Response
{
    public class SearchDocumentResponseExternal
    {
        public string Query { get; set; }
        public string Answer { get; set; }
        public List<RelevantSourceResponseExternal> RelevantSources { get; set; } = new List<RelevantSourceResponseExternal>();
        public bool NoResult { get; set; }
    }
}

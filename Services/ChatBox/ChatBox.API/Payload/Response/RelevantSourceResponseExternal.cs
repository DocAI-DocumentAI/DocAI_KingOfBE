namespace ChatBox.API.Payload.Response
{
    public class RelevantSourceResponseExternal
    {
        public string FileName { get; set; }
        public string TextSnippet { get; set; }
        public double Relevance { get; set; }
        public string SourceUrl { get; set; }
    }
}

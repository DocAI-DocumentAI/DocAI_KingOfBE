namespace ChatBox.API.Payload.Response.DocumentServiceResponse
{
    public class SearchSuggestionsResponse
    {
        public List<string> Suggestions { get; set; } = new();
        public string OriginalQuery { get; set; }
        public Dictionary<string, int> PopularTerms { get; set; } = new();
        public List<string> RelatedTopics { get; set; } = new();
    }
}

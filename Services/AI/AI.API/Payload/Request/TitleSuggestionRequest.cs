namespace AI.API.Payload.Request
{
    public class TitleSuggestionRequest
    {
        public string Content { get; set; }
        public int? MaxLength { get; set; } = 50;
        public string? Language { get; set; } = "vi";
    }
}

namespace AI.API.Payload.Response
{
    public class AIResponse
    {
        public string Answer { get; set; }
        public string ModelUsed { get; set; }
        // REVIEW POINT: 'TotalTokens' error fix:
        // OllamaSharp's ChatResponseStream (chunk) doesn't directly expose TotalTokens.
        // It's usually part of the final 'done' chunk or requires a tokenizer.
        // For simplicity and to fix the error, we'll remove it from AIResponse for now.
        // If you need it, you'll have to count tokens using a tokenizer library
        // or parse the final 'done' chunk's metadata.
        // public int TotalTokens { get; set; } // Removed to fix error
    }
}

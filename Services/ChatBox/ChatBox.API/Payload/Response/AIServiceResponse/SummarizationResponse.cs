namespace ChatBox.API.Payload.Response.AIServiceResponse
{
    public class SummarizationResponse
    {
        public string Summary { get; set; }
        public int OriginalLength { get; set; }
        public int SummaryLength { get; set; }
    }
}

namespace ChatBox.API.Payload.Response.AIServiceResponse
{
    public class TranslationResponse
    {
        public string TranslatedText { get; set; }
        public string DetectedSourceLanguage { get; set; }
        public double Confidence { get; set; }
    }
}

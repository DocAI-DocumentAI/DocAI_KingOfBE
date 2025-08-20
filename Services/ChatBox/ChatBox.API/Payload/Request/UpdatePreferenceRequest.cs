namespace ChatBox.API.Payload.Request
{
    public class UpdatePreferenceRequest
    {
        public string? UserName { get; set; }
        public List<string>? ChatbotCharacteristics { get; set; }
        public string? AdditionalInfo { get; set; }
    }
}

namespace ChatBox.API.Payload.Request
{
    public class UserPreferenceRequest
    {
        public string UserName { get; set; }
        public List<string> ChatbotCharacteristics { get; set; } = new();
        public string AdditionalInfo { get; set; }
    }
}

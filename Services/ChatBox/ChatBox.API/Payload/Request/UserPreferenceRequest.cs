namespace ChatBox.API.Payload.Request
{
    public class UserPreferenceRequest
    {
        public string? UserName { get; set; }
        public List<string>? ChatbotCharacteristics { get; set; }
        public string? AdditionalInfo { get; set; }
        public bool ApplyToNewChats { get; set; } = false; 
    }
}

namespace ChatBox.API.Payload.Response
{
    public class UserPreferenceResponse
    {
        public string UserName { get; set; }
        public List<string> ChatbotCharacteristics { get; set; } = new();
        public string AdditionalInfo { get; set; }
        public List<CharacteristicOption> AvailableCharacteristics { get; set; } = new();
    }
    public class CharacteristicOption
    {
        public string Value { get; set; }
        public string DisplayName { get; set; }
    }
}

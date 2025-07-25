namespace ChatBox.API.Payload.Response.UserPreferenceResponse
{
    public class PreferenceValidationInfo
    {
        public bool IsValid { get; set; }
        public List<string> ValidationErrors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> SuggestedValues { get; set; } = new();
    }
}

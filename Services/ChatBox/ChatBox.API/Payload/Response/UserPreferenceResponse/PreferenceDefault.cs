namespace ChatBox.API.Payload.Response.UserPreferenceResponse
{
    public class PreferenceDefault
    {
        public string Name { get; set; }
        public object Value { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string DataType { get; set; }
        public bool IsUserConfigurable { get; set; }
        public Dictionary<string, object> ValidationRules { get; set; } = new();
        public List<object> AllowedValues { get; set; } = new();
    }
}

using System.ComponentModel.DataAnnotations;

namespace ChatBox.API.Payload.Request.UserPreferenceService
{
    public class SetDefaultPreferencesRequest
    {
        [Required]
        public string SettingName { get; set; }

        [Required]
        public object DefaultValue { get; set; }

        public string Description { get; set; }
        public string Category { get; set; }
        public bool IsUserConfigurable { get; set; } = true;
        public Dictionary<string, object> ValidationRules { get; set; } = new();
    }
}

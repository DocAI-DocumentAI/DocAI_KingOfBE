namespace Auth.API.Payload.Request.User
{
    public class UpdateUserSettingRequest
    {
        public bool TwoFactorEnabled { get; set; }
        public string? TwoFactorMethod { get; set; }
        public bool NotificationsEnabled { get; set; }
    }
}
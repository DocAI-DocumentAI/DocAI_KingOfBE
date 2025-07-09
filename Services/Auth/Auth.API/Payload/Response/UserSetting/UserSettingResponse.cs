namespace Auth.API.Payload.Response.UserSetting;

public class UserSettingResponse
{
    public Guid Id { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public string? TwoFactorMethod { get; set; }
    public bool NotificationsEnabled { get; set; }
    public DateTime UpdateAt { get; set; }
}
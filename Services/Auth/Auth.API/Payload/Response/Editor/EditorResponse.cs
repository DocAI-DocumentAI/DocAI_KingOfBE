namespace Auth.API.Payload.Response.Staff;

public class EditorResponse
{
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? FullName { get; set; }
    public bool? TwoFactorEnabled { get; set; }
    public string? TwoFactorMethod { get; set; }
    public string? Type { get; set; }
}
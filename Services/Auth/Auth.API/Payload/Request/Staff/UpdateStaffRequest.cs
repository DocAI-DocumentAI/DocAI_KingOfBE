namespace Auth.API.Payload.Request.Staff;

public class UpdateStaffRequest
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Type { get; set; }
    public bool? TwoFactorEnabled { get; set; }
    public string? TwoFactorMethod { get; set; }
}
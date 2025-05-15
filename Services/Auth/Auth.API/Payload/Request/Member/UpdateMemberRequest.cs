namespace Auth.API.Payload.Request.Member;

public class UpdateMemberRequest
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public bool? TwoFactorEnabled { get; set; }
    public string? TwoFactorMethod { get; set; }
}
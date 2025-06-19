namespace Auth.API.Payload.Response.ActiveKey;

public class ActiveKeyResponse
{
    public string ActivationCode { get; set; }
    public string RoleName { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
}
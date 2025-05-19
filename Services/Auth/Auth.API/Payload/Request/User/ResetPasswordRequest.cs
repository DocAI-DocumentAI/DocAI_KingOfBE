namespace Auth.API.Payload.Request;

public class ResetPasswordRequest
{
    public string passwordOld { get; set; }
    public string passwordNew { get; set; }
    public string passwordConfirm { get; set; }
}
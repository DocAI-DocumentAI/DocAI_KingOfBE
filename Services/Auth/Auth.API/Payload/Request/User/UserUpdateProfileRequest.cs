namespace Auth.API.Payload.Request.User
{
    public class UserUpdateProfileRequest
    {
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
    }
}
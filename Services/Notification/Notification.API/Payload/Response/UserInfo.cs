namespace Notification.API.Payload.Response
{
    public class UserInfo
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string? DepartmentId { get; set; } = string.Empty;

    }
}

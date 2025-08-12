namespace Notification.API.Utils
{
    /// <summary>
    /// Data transfer object for user information used in notifications
    /// </summary>
    public class UserInfo
    {
        public string? UserId { get; set; }
        public string? Email { get; set; }
        public string? Name { get; set; }
        public string? Department { get; set; }
        public string? DepartmentId { get; set; }
        public string? Role { get; set; }
    }
}

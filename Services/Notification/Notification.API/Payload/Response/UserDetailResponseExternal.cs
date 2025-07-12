namespace Notification.API.Payload.Response
{
    public class UserDetailResponseExternal
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? Phone { get; set; }
        public Guid? RoleId { get; set; }
        public string? RoleName { get; set; }
        public Guid? DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
    }
}

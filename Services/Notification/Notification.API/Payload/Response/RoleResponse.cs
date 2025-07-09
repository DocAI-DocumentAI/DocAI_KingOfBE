namespace Notification.API.Payload.Response
{
    public class RoleResponse
    {
        public Guid Id { get; set; }
        public string RoleName { get; set; } = null!;
        public string? Description { get; set; }
    }
}

namespace Auth.API.Payload.Request.User
{
    public class AdminUpdateUserRequest
    {
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public Guid? RoleId { get; set; }
        public Guid? DepartmentId { get; set; }
        public bool? Active { get; set; }
        public bool? RequirePasswordChange { get; set; }
        public List<Guid>? PermissionIds { get; set; }
    }
}
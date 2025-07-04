using Auth.API.Payload.Response.Staff;

namespace Auth.API.Payload.Response.ActiveKey
{
    public class ActiveKeyListResponse
    {
        public Guid Id { get; set; }
        public string ActivationCode { get; set; }
        public string Status { get; set; }
        public RoleResponse Role { get; set; }
        public DepartmentResponse Department { get; set; }
        public UserSummaryResponse? CreatedByUser { get; set; }
        public UserSummaryResponse? UsedByUser { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class UserSummaryResponse
    {
        public Guid Id { get; set; }
        public string UserName { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
    }
}

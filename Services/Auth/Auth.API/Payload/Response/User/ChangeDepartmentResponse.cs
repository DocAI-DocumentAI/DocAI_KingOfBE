using Auth.API.Payload.Response.Department;
using Auth.API.Payload.Response.Role;

namespace Auth.API.Payload.Response.User
{
    public class ChangeDepartmentResponse
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; }
        public string FullName { get; set; }
        public DepartmentResponse OldDepartment { get; set; }
        public DepartmentResponse NewDepartment { get; set; }
        public RoleResponse Role { get; set; }
        public DateTime ChangeDate { get; set; }
    }
}

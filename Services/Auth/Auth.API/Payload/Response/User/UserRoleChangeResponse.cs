using Auth.API.Payload.Response.Staff;
using System;

namespace Auth.API.Payload.Response
{
    public class UserRoleChangeResponse
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; }
        public RoleResponse OldRole { get; set; }
        public RoleResponse NewRole { get; set; }
        public DepartmentResponse Department { get; set; }
        public DateTime ChangeDate { get; set; }
        public string Token { get; set; }
    }
}
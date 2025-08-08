using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Command
{
    public class GetDepartmentUsersCommand
    {
        public Guid DepartmentId { get; set; }
        public string? RoleFilter { get; set; }
        public Guid RequestId { get; set; } = Guid.NewGuid();
    }
}

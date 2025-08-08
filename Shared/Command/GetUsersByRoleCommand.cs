using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Command
{
    public class GetUsersByRoleCommand
    {
        public string RoleName { get; set; } = null!;
        public Guid RequestId { get; set; } = Guid.NewGuid();
    }
}

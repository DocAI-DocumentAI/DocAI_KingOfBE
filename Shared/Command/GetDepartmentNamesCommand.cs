using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Command
{
    public class GetDepartmentNamesCommand
    {
        public List<Guid> DepartmentIds { get; set; } = new();
        public Guid RequestId { get; set; } = Guid.NewGuid();
    }
}

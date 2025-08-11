using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Command
{
    public class GetUsersNotificationPreferencesCommand
    {
        public List<Guid> UserIds { get; set; } = new();
        public Guid RequestId { get; set; } = Guid.NewGuid();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Command
{
    public class GetUserNotificationPreferencesCommand
    {
        public Guid UserId { get; set; }
        public Guid RequestId { get; set; } = Guid.NewGuid();

    }
}

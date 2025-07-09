using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Enum
{
    public enum DeliveryChannel
    {
        None = 0,
        Email = 1,
        SystemAlert = 2,
        All = Email | SystemAlert
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Enum
{
    public enum MessageType
    {
        Text = 1,
        System = 2,
        Error = 3,
        Warning = 4,
        Info = 5
    }
}

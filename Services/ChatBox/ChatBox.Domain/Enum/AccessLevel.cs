using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Enum
{
    public enum AccessLevel
    {
        Public = 1,
        Internal = 2,
        Confidential = 3,
        Restricted = 4,
        TopSecret = 5
    }
}

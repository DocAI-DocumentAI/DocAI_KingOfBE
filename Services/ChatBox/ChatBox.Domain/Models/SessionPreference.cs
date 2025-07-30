using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class SessionPreference : BaseEntity
    {
        public string Key { get; set; }
        public string Value { get; set; }
        public string SessionId { get; set; }
        public virtual ChatSession Session { get; set; }
    }
}

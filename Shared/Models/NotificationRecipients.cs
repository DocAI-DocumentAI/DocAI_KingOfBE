using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Models
{
    public class NotificationRecipients
    {
        public List<Guid>? UserIds { get; set; }
        public string? RoleName { get; set; } 
        public Guid? DepartmentId { get; set; }
        public List<string>? EmailAddresses { get; set; }
    }
}

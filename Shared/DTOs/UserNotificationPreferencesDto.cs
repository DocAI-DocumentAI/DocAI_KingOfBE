using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs
{
    public class UserNotificationPreferencesDto
    {
        public Guid UserId { get; set; }
        public bool NotificationsEnabled { get; set; }
        public bool EmailNotificationsEnabled { get; set; } = true;
        public bool SystemNotificationsEnabled { get; set; } = true;
        public bool DocumentWorkflowEnabled { get; set; } = true;
        public bool DocumentExpirationEnabled { get; set; } = true;
        public bool DocumentSubmissionEnabled { get; set; } = true;
        public bool DocumentApprovalEnabled { get; set; } = true;
        public bool DocumentRejectionEnabled { get; set; } = true;
    }
}

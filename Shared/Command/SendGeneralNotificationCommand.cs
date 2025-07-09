using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shared.Enum;
using Shared.Models;

namespace Shared.Command
{
    public class SendGeneralNotificationCommand
    {
        public DeliveryChannel Channels { get; set; } = DeliveryChannel.All;

        public string TemplateName { get; set; } = null!;
        public Dictionary<string, string>? TemplateData { get; set; }
        public NotificationRecipients Recipients { get; set; } = null!;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs
{
    public class GetUsersNotificationPreferencesResponse
    {
        public List<UserNotificationPreferencesDto> Preferences { get; set; } = new();
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Guid RequestId { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class SystemPreference
    {
        public Guid Id { get; set; }
        public string SettingName { get; set; }
        public string DefaultValue { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string DataType { get; set; }
        public bool IsUserConfigurable { get; set; }
        public string ValidationRules { get; set; } // JSON string
        public string AllowedValues { get; set; } // JSON string
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;
    }
}

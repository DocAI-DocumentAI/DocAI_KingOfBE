using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shared.Models;

namespace Shared.DTOs
{
    public class GetExpiringDocumentsResponse
    {
        public List<DocumentExpirationDto> Documents { get; set; } = new();
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Guid RequestId { get; set; }
    }
}

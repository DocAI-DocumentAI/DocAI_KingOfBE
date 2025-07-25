using AutoMapper;
using ChatBox.Domain.Models;

namespace ChatBox.API.Mappers
{
    public class SecurityMapper : Profile
    {
        public SecurityMapper()
        {
            // Basic security mappings
            CreateMap<SecurityAuditLog, object>()
                .ConvertUsing(src => new
                {
                    EventId = src.Id.ToString(),
                    EventType = src.EventType,
                    UserId = src.UserId,
                    Timestamp = src.Timestamp,
                    Severity = src.Severity.ToString()
                });


        }
    }
}

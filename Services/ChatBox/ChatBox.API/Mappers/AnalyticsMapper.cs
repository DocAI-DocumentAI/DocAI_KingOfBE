using AutoMapper;
using ChatBox.API.Payload.Request.ChatService;
using ChatBox.Domain.Models;

namespace ChatBox.API.Mappers
{
    public class AnalyticsMapper : Profile
    {
        public AnalyticsMapper()
        {
            // Basic mappings for analytics
            CreateMap<ChatSession, object>()
                .ConvertUsing(src => new
                {
                    SessionId = src.Id.ToString(),
                    MessageCount = src.MessageCount,
                    Duration = src.LastActivityAt - src.CreatedAt,
                    CreatedAt = src.CreatedAt,
                    LastActivityAt = src.LastActivityAt
                });

        }
    }
}

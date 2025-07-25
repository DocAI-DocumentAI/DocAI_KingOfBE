using AutoMapper;
using ChatBox.Domain.Models;

namespace ChatBox.API.Mappers
{
    public class ContentModerationMapper : Profile
    {
        public ContentModerationMapper()
        {
            // Basic content moderation mappings
            CreateMap<ContentModerationRule, object>()
                .ConvertUsing(src => new
                {
                    Id = src.Id.ToString(),
                    Name = src.Name,
                    IsActive = src.IsActive,
                    CreatedAt = src.CreatedAt
                });


        }
    }
}

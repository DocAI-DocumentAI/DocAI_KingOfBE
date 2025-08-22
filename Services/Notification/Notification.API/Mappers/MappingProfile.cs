using AutoMapper;
using Notification.API.Payload.Request;
using Notification.API.Payload.Response;
using Notification.Domain.Models;
using Notification.Infrastructure.Paginate;

namespace Notification.API.Mappers
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<EmailTemplateRequest, EmailTemplate>();
            CreateMap<EmailTemplate, EmailTemplateResponse>();

            // ✅ UPDATED: Notification Config mappings với fields mới
            CreateMap<NotificationConfigRequest, NotificationConfig>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.ConfigKey, opt => opt.Ignore())
                .ForMember(dest => dest.CreateAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdateAt, opt => opt.Ignore());

            CreateMap<NotificationConfig, NotificationConfigResponse>()
                .ForMember(dest => dest.NextExpiredNotificationTime, opt => opt.Ignore())
                .ForMember(dest => dest.NextNearExpiredNotificationTime, opt => opt.Ignore())
                .ForMember(dest => dest.NearExpiredModeDescription, opt => opt.MapFrom(src => src.NearExpiredMode.ToString()));

            // ✅ EXISTING: Notification Log mappings
            CreateMap<NotificationLog, NotificationResponse>()
                .ForMember(dest => dest.IsRead, opt => opt.MapFrom(src => src.IsRead))
                .ForMember(dest => dest.ReadAt, opt => opt.MapFrom(src => src.ReadAt));
        }

    }
}
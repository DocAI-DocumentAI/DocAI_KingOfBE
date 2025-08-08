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

            CreateMap<NotificationConfigRequest, NotificationConfig>();
            CreateMap<NotificationConfig, NotificationConfigResponse>();

            CreateMap<NotificationLog, NotificationResponse>();
        }

    }
}
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
            CreateMap<NotificationConfigRequest, NotificationConfig>();

            CreateMap<EmailTemplate, EmailTemplateResponse>();
            CreateMap<NotificationConfig, NotificationConfigResponse>();
            CreateMap<NotificationLog, NotificationResponse>();

            CreateMap(typeof(IPaginate<>), typeof(IPaginate<>)).ConvertUsing(typeof(PaginateConverter<,>));
        }

    }
}
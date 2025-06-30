using Auth.API.Payload.Response.ActiveKey;
using Auth.Domain.Models;
using AutoMapper;

namespace Auth.API.Mappers
{
    public class ActiveKeyMapper : Profile
    {
        public ActiveKeyMapper()
        {
            CreateMap<ActiveKey, ActiveKeyResponse>();
            CreateMap<ActiveKey, ActiveKeyListResponse>();
        }
    }
}

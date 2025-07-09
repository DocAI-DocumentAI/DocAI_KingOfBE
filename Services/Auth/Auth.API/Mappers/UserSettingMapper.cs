using Auth.API.Payload.Response.UserSetting;
using Auth.Domain.Models;
using AutoMapper;

namespace Auth.API.Mappers;

public class UserSettingMapper : Profile
{
    public UserSettingMapper()
    {
        CreateMap<UserSetting, UserSettingResponse>();
    }
}
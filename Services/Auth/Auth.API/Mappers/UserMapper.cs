using Auth.API.Models;
using Auth.API.Payload.Request;
using Auth.API.Payload.Response;
using AutoMapper;

namespace Auth.API.Mappers;

public class UserMapper : Profile
{
    public UserMapper()
    {
        CreateMap<RegisterRequest, User>();
        CreateMap<User, RegisterResponse>();
    }   
}
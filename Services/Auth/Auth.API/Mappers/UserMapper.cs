using Auth.API.Payload.Request;
using Auth.API.Payload.Response;
using Auth.Domain.Models;
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
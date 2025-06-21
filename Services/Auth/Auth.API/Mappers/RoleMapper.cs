using Auth.API.Payload.Request.Staff;
using Auth.API.Payload.Response.Staff;
using Auth.Domain.Models;
using AutoMapper;

namespace Auth.API.Mappers;

public class RoleMapper : Profile
{
    public RoleMapper()
    {
        CreateMap<Role,RoleResponse>();
    }
}
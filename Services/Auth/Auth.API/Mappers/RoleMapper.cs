using Auth.API.Payload.Response.Role;
using Auth.Domain.Models;
using AutoMapper;

namespace Auth.API.Mappers;

public class RoleMapper : Profile
{
    public RoleMapper()
    {
        CreateMap<Role, RoleResponse>();
    }
}
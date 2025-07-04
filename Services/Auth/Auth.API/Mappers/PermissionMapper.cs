using Auth.API.Payload.Response.Permission;
using Auth.Domain.Models;
using AutoMapper;

namespace Auth.API.Mappers;

public class PermissionMapper : Profile
{
    public PermissionMapper()
    {
        CreateMap<Permission, PermissionResponse>();
    }   
}
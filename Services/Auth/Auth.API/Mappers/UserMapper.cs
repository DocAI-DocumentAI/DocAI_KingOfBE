using Auth.API.Payload.Request;
using Auth.API.Payload.Response;
using Auth.API.Payload.Response.Department;
using Auth.API.Payload.Response.Permission;
using Auth.API.Payload.Response.Role;
using Auth.API.Payload.Response.User;
using Auth.Domain.Models;
using AutoMapper;

namespace Auth.API.Mappers;

public class UserMapper : Profile
{
    public UserMapper()
    {
        CreateMap<RegisterRequest, User>();
        CreateMap<User, RegisterResponse>();

        CreateMap<User, UserResponse>()
            .ForMember(dest => dest.Role, opt => opt.MapFrom(src => new RoleResponse
            {
                Id = src.Role.Id,
                RoleName = src.Role.RoleName,
                Description = src.Role.Description,
                CreateAt = src.Role.CreateAt,
                UpdateAt = src.Role.UpdateAt
            }))
            .ForMember(dest => dest.Department, opt => opt.MapFrom(src => new DepartmentResponse
            {
                Id = src.Department.Id,
                Name = src.Department.Name,
                Description = src.Department.Description,
                CreateAt = src.Department.CreateAt,
                UpdateAt = src.Department.UpdateAt
            }))
            .ForMember(dest => dest.Permissions, opt => opt.MapFrom(src =>
                src.UserPermissions.Select(up => new PermissionResponse
                {
                    Id = up.Permission.Id,
                    Name = up.Permission.Name,
                    Description = up.Permission.Description,
                    CreateAt = up.Permission.CreateAt,
                    UpdateAt = up.Permission.UpdateAt
                })))
            .ForMember(dest => dest.UserSetting, opt => opt.Ignore());
    }
}

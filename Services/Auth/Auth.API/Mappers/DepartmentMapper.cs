using Auth.API.Payload.Response;
using Auth.API.Payload.Response.Department;
using Auth.Domain.Models;
using AutoMapper;

namespace Auth.API.Mappers;

public class DepartmentMapper : Profile
{
    public DepartmentMapper()
    {
        CreateMap<Department, DepartmentResponse>();
    }
}
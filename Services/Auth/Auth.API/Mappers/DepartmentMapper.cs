using Auth.API.Payload.Response;
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
using Auth.API.Payload.Response.Staff;
using Auth.Domain.Models;
using AutoMapper;

namespace Auth.API.Mappers;

public class StaffMapper : Profile
{
    public StaffMapper()
    {
        CreateMap<Staff,StaffResponse>();
    }
}
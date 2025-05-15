using Auth.API.Models;
using Auth.API.Payload.Response.Staff;
using AutoMapper;

namespace Auth.API.Mappers;

public class StaffMapper : Profile
{
    public StaffMapper()
    {
        CreateMap<Staff,StaffResponse>();
    }
}
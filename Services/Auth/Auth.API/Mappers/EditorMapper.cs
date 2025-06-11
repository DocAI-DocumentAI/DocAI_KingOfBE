using Auth.API.Payload.Response.Staff;
using Auth.Domain.Models;
using AutoMapper;

namespace Auth.API.Mappers;

public class EditorMapper : Profile
{
    public EditorMapper()
    {
        CreateMap<Editor,EditorResponse>();
    }
}
using Auth.API.Payload.Request;
using Auth.API.Payload.Response;
using Auth.Domain.Models;
using AutoMapper;

namespace Auth.API.Mappers;

public class MemberMapper : Profile
{
    public MemberMapper()
    {
        CreateMap<Member, MemberResponse>();
    }   
}
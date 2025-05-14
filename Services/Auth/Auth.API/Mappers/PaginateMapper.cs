using Auth.API.Paginate;
using AutoMapper;

namespace Auth.API.Mappers;

public class PaginateMapper : Profile
{
    public PaginateMapper()
    {
        CreateMap(typeof(IPaginate<>), typeof(IPaginate<>)).ConvertUsing(typeof(PaginateConverter<,>));
    }
}
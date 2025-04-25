using AutoMapper;

namespace Auth.API.Paginate;

public class PaginateConverter<TSource, TDestination> : ITypeConverter<IPaginate<TSource>, IPaginate<TDestination>>
{
    public IPaginate<TDestination> Convert(IPaginate<TSource> source, IPaginate<TDestination> destination, ResolutionContext context)
    {
        var mappedItems = context.Mapper.Map<IList<TDestination>>(source.Items);
        return new Paginate<TDestination>
        {
            Items = mappedItems,
            Page = source.Page,
            Size = source.Size,
            Total = source.Total,
            TotalPages = source.TotalPages
        };
    }
}
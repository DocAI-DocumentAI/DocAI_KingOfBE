namespace Auth.API.Paginate;

public class Paginate<TResult> : IPaginate<TResult>
{
    public int Size { get; set; }
    public int Page { get; set; }
    public int Total { get; set; }
    public int TotalPages { get; set; }
    public IList<TResult> Items { get; set; }
    
    public Paginate()
    {
        Items = Array.Empty<TResult>();
    }
}
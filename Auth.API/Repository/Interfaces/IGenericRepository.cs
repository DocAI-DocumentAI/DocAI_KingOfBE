using System.Linq.Expressions;
using System.Reflection;
using Auth.API.Filter;
using Auth.API.Paginate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Auth.API.Repository.Interfaces;

public abstract class IGenericRepository<T> : IDisposable where T : class
{
    protected readonly DbSet<T> _dbSet;

    public abstract Task<T> SingleOrDefaultAsync(Expression<Func<T, bool>> predicate = default(Expression<Func<T, bool>>),
        Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = default(Func<IQueryable<T>, IOrderedQueryable<T>>),
        Func<IQueryable<T>, IIncludableQueryable<T, object>> include =
            default(Func<IQueryable<T>, IIncludableQueryable<T, object>>));

    public abstract Task<TResult> SingleOrDefaultAsync<TResult>(Expression<Func<T, TResult>> selector,
        Expression<Func<T, bool>> predicate = default(Expression<Func<T, bool>>),
        Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = default(Func<IQueryable<T>, IOrderedQueryable<T>>),
        Func<IQueryable<T>, IIncludableQueryable<T, object>> include =
            default(Func<IQueryable<T>, IIncludableQueryable<T, object>>));

    public abstract Task<ICollection<T>> GetListAsync(Expression<Func<T, bool>> predicate = default(Expression<Func<T, bool>>),
        Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = default(Func<IQueryable<T>, IOrderedQueryable<T>>),
        Func<IQueryable<T>, IIncludableQueryable<T, object>> include =
            default(Func<IQueryable<T>, IIncludableQueryable<T, object>>));

    public abstract Task<ICollection<TResult>> GetListAsync<TResult>(Expression<Func<T, TResult>> selector,
        Expression<Func<T, bool>> predicate = default(Expression<Func<T, bool>>),
        Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = default(Func<IQueryable<T>, IOrderedQueryable<T>>),
        Func<IQueryable<T>, IIncludableQueryable<T, object>> include =
            default(Func<IQueryable<T>, IIncludableQueryable<T, object>>));

    public virtual async Task<IPaginate<TResult>> GetPagingListAsync<TResult>(Expression<Func<T, TResult>> selector, IFilter<T> filter, Expression<Func<T, bool>> predicate = null, Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
        Func<IQueryable<T>, IIncludableQueryable<T, object>> include = null, int page = 1, int size = 10, string sortBy = null, bool isAsc = true)
    {
        IQueryable<T> query = _dbSet;
        
        if (filter != null)
        {
            var filterExpression = filter.ToExpression();
            query = query.Where(filterExpression);
        }
        if (predicate != null) query = query.Where(predicate);
        if (include != null) query = include(query);
        if (!string.IsNullOrEmpty(sortBy))
        {
            query = ApplySort(query, sortBy, isAsc);
        }
        else if (orderBy != null)
        {
            query = orderBy(query);
        }
        
        return await query.AsNoTracking().Select(selector).ToPaginateAsync(page, size, 1);
      
    }
    private IQueryable<T> ApplySort(IQueryable<T> query, string sortBy, bool isAsc)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var property = typeof(T).GetProperty(sortBy, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (property == null)
        {
            throw new ArgumentException($"Property '{sortBy}' not found on type {typeof(T).Name}");
        }
        var propertyAccess = Expression.Property(parameter, property);
        var lambda = Expression.Lambda(propertyAccess, parameter);
         
        string methodName = isAsc ? "OrderBy" : "OrderByDescending";

        var resultExpression = Expression.Call(typeof(Queryable), methodName, 
            new Type[] {typeof(T), propertyAccess.Type},
            query.Expression, Expression.Quote(lambda));
        return query.Provider.CreateQuery<T>(resultExpression);
    }

    public abstract Task InsertAsync(T entity);
    public abstract Task InsertRangeAsync(IEnumerable<T> entities);
    public abstract void UpdateAsync(T entity);
    public abstract void UpdateRange(IEnumerable<T> entities);
    public abstract void DeleteAsync(T entity);
    public abstract void DeleteRangeAsync(IEnumerable<T> entities);
    public abstract void Dispose();
    
}
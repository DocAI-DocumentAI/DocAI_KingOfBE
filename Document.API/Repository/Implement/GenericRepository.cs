using System.Linq.Expressions;
using System.Reflection;
using Auth.API.Filter;
using Auth.API.Paginate;
using Auth.API.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Auth.API.Repository.Implement;

public class GenericRepository<T> : IGenericRepository<T> where T : class
	{
		protected readonly DbContext _dbContext;
		protected readonly DbSet<T> _dbSet;

		public GenericRepository(DbContext context)
		{
			_dbContext = context;
			_dbSet = context.Set<T>();
		}

		public override void Dispose()
		{
			_dbContext?.Dispose();
		}

		#region Gett Async

		public override async Task<T> SingleOrDefaultAsync(Expression<Func<T, bool>> predicate = null, Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null, Func<IQueryable<T>, IIncludableQueryable<T, object>> include = null)
		{
			IQueryable<T> query = _dbSet;
			if (include != null) query = include(query);

			if (predicate != null) query = query.Where(predicate);

			if (orderBy != null) return await orderBy(query).AsNoTracking().FirstOrDefaultAsync();

			return await query.AsNoTracking().FirstOrDefaultAsync();
		}

		public override async Task<TResult> SingleOrDefaultAsync<TResult>(Expression<Func<T, TResult>> selector, Expression<Func<T, bool>> predicate = null, Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
			Func<IQueryable<T>, IIncludableQueryable<T, object>> include = null)
		{
			IQueryable<T> query = _dbSet;
			if (include != null) query = include(query);

			if (predicate != null) query = query.Where(predicate);

			if (orderBy != null) return await orderBy(query).AsNoTracking().Select(selector).FirstOrDefaultAsync();

			return await query.AsNoTracking().Select(selector).FirstOrDefaultAsync();
		}

		public override async Task<ICollection<T>> GetListAsync(Expression<Func<T, bool>> predicate = null, Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null, Func<IQueryable<T>, IIncludableQueryable<T, object>> include = null)
		{
			IQueryable<T> query = _dbSet;

			if (include != null) query = include(query);

			if (predicate != null) query = query.Where(predicate);

			if (orderBy != null) return await orderBy(query).AsNoTracking().ToListAsync();

			return await query.AsNoTracking().ToListAsync();
		}

		public override async Task<ICollection<TResult>> GetListAsync<TResult>(Expression<Func<T, TResult>> selector, Expression<Func<T, bool>> predicate = null, Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null, Func<IQueryable<T>, IIncludableQueryable<T, object>> include = null)
		{
			IQueryable<T> query = _dbSet;

			if (include != null) query = include(query);

			if (predicate != null) query = query.Where(predicate);

			if (orderBy != null) return await orderBy(query).AsNoTracking().Select(selector).ToListAsync();

			return await query.Select(selector).ToListAsync();
		}

		public override async Task<IPaginate<TResult>> GetPagingListAsync<TResult>(Expression<Func<T, TResult>> selector, IFilter<T> filter, Expression<Func<T, bool>> predicate = null, Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
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

		#endregion

		#region Insert

		public override async Task InsertAsync(T entity)
		{
			if (entity == null) return;
			await _dbSet.AddAsync(entity);
		}

		public override async Task InsertRangeAsync(IEnumerable<T> entities)
		{
			await _dbSet.AddRangeAsync(entities);
		}

		#endregion

		#region Update
		public override void UpdateAsync(T entity)
		{
			_dbSet.Update(entity);
		}

		public override void UpdateRange(IEnumerable<T> entities)
		{
			_dbSet.UpdateRange(entities);
		}

		public override void DeleteAsync(T entity)
		{
			_dbSet.Remove(entity);
		}

		public override void DeleteRangeAsync(IEnumerable<T> entities)
		{
			_dbSet.RemoveRange(entities);
		}

		#endregion
	}
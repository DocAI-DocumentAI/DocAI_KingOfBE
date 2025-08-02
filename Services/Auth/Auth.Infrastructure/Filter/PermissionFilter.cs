using System.Linq.Expressions;
using Auth.Domain.Models;

namespace Auth.Infrastructure.Filter;

public class PermissionFilter : IFilter<Permission>
{
    public string? Description { get; set; }
    public string? PermissionName { get; set; }

    public Expression<Func<Permission, bool>> ToExpression()
    {
        return Permission =>
            (string.IsNullOrEmpty(PermissionName) || Permission.Name.Contains(PermissionName)) &&
            (string.IsNullOrEmpty(Description) || Permission.Description.Contains(Description));
    }
}
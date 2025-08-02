using System.Linq.Expressions;
using Auth.Domain.Models;

namespace Auth.Infrastructure.Filter;

public class UserFilter : IFilter<User>
{
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public Guid? RoleId { get; set; }
    public Guid? DepartmentId { get; set; }
    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedTo { get; set; }
    public Guid? PermissionId { get; set; }

    public Expression<Func<User, bool>> ToExpression()
    {
        var createdFromUtc = CreatedFrom.HasValue ?
            DateTime.SpecifyKind(CreatedFrom.Value, DateTimeKind.Utc) : (DateTime?)null;
        var createdToUtc = CreatedTo.HasValue ?
            DateTime.SpecifyKind(CreatedTo.Value, DateTimeKind.Utc) : (DateTime?)null;
        return user =>
            (string.IsNullOrEmpty(Email) || user.Email.Contains(Email)) &&
            (string.IsNullOrEmpty(FullName) || user.FullName.Contains(FullName)) &&
            (string.IsNullOrEmpty(Phone) || user.Phone.Contains(Phone)) &&
            (!RoleId.HasValue || user.RoleId == RoleId) &&
            (!DepartmentId.HasValue || user.DepartmentId == DepartmentId) &&
            (!createdFromUtc.HasValue || user.CreatAt >= createdFromUtc.Value) &&
            (!createdToUtc.HasValue || user.CreatAt <= createdToUtc.Value) &&
            (!PermissionId.HasValue || user.UserPermissions.Any(up => up.PermissionId == PermissionId));
    }
}



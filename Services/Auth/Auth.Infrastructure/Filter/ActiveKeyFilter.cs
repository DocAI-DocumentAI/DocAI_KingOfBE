// using System.Linq.Expressions;
// using Auth.Domain.Models;
//
// namespace Auth.Infrastructure.Filter
// {
//     public class ActiveKeyFilter : IFilter<ActiveKey>
//     {
//         public string? ActivationCode { get; set; }
//         public string? Status { get; set; }
//         public Guid? RoleId { get; set; }
//         public Guid? DepartmentId { get; set; }
//         public Guid? CreatedByUserId { get; set; }
//         public Guid? UsedByUserId { get; set; }
//         public DateTime? CreatedFrom { get; set; }
//         public DateTime? CreatedTo { get; set; }
//
//         public Expression<Func<ActiveKey, bool>> ToExpression()
//         {
//             return activeKey =>
//                 (string.IsNullOrEmpty(ActivationCode) || activeKey.ActivationCode.Contains(ActivationCode)) &&
//                 (string.IsNullOrEmpty(Status) || activeKey.Status.Contains(Status)) &&
//                 (!RoleId.HasValue || activeKey.RoleId == RoleId.Value) &&
//                 (!DepartmentId.HasValue || activeKey.DepartmentId == DepartmentId.Value) &&
//                 (!CreatedByUserId.HasValue || activeKey.CreatedByUserId == CreatedByUserId.Value) &&
//                 (!UsedByUserId.HasValue || activeKey.UsedByUserId == UsedByUserId.Value) &&
//                 (!CreatedFrom.HasValue || activeKey.CreatedAt >= CreatedFrom.Value) &&
//                 (!CreatedTo.HasValue || activeKey.CreatedAt <= CreatedTo.Value);
//         }
//     }
// }

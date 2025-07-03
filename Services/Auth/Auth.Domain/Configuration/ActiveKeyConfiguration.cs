// using Auth.Domain.Models;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.EntityFrameworkCore.Metadata.Builders;
//
// namespace Auth.Domain.Configuration;
//
// public class ActiveKeyConfiguration : IEntityTypeConfiguration<ActiveKey>
// {
//     public void Configure(EntityTypeBuilder<ActiveKey> builder)
//     {
//         // Cấu hình khóa chính
//         builder.HasKey(a => a.Id);
//
//         // Cấu hình mối quan hệ với User thông qua UsedByUserId
//         builder.HasOne(a => a.UsedByUser)
//             .WithMany()
//             .HasForeignKey(a => a.UsedByUserId)
//             .IsRequired(false)
//             .OnDelete(DeleteBehavior.Restrict);
//
//         // Cấu hình mối quan hệ với User thông qua CreatedByUserId
//         builder.HasOne(a => a.CreatedByUser)
//             .WithMany(u => u.ActiveKeys)
//             .HasForeignKey(a => a.CreatedByUserId)
//             .IsRequired()
//             .OnDelete(DeleteBehavior.Restrict);
//
//         // Cấu hình mối quan hệ với Role
//         builder.HasOne(a => a.Role)
//             .WithMany()
//             .HasForeignKey(a => a.RoleId)
//             .IsRequired()
//             .OnDelete(DeleteBehavior.Restrict);
//
//         // Cấu hình mối quan hệ với Department
//         builder.HasOne(a => a.Department)
//             .WithMany()
//             .HasForeignKey(a => a.DepartmentId)
//             .IsRequired()
//             .OnDelete(DeleteBehavior.Restrict);
//
//         // Dữ liệu mẫu (nếu có)
//         builder.HasData(
//             new ActiveKey
//             {
//                 Id = Guid.Parse("50b64957-bae3-4377-aa7a-fee36d25ccd6"),
//                 ActivationCode = "P4rBZtdXa5YvEGJNmKLcQq7RfW9HU61o",
//                 Status = "On",
//                 UsedByUserId = null,
//                 CreatedByUserId = Guid.Parse("595dd357-aaec-455e-9fa7-4fc88d4b819c"), //Manager
//                 RoleId = Guid.Parse("4e29a870-9131-4cc2-97ca-eaa748b5f17f"), //Member
//                 DepartmentId = Guid.Parse("8bf13891-1ce9-405c-add9-0ada93308671"), // DepartentA
//                 CreatedAt = DateTime.UtcNow,
//                 UpdatedAt = DateTime.UtcNow
//             }
//             // Thêm các dữ liệu mẫu khác nếu cần
//         );
//     }
// }

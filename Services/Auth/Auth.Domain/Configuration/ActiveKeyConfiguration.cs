using Auth.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auth.Domain.Configuration;

public class ActiveKeyConfiguration : IEntityTypeConfiguration<ActiveKey>
{
    public void Configure(EntityTypeBuilder<ActiveKey> builder)
    {
        builder.HasData(
            new ActiveKey {
                Id = Guid.Parse("50b64957-bae3-4377-aa7a-fee36d25ccd6"),
                ActivationCode = "P4rBZtdXa5YvEGJNmKLcQq7RfW9HU61o",
                RoleName = "Editor"
            },
            new ActiveKey {
                Id = Guid.Parse("65de7f7d-0bcc-4cdf-bd8c-f8d1ac290cd8"),
                ActivationCode = "zXYmN7pLcVTEqF59jKADrCbhQuU630aw",
                RoleName = "Manager"
            }
        );
    }
}

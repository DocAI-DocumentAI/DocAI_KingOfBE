using FluentValidation;
using Auth.API.Payload.Request.Permission;

namespace Auth.API.Validators.Permission;

public class UpdatePermissionRequestValidator : AbstractValidator<UpdatePermissionRequest>
{
    public UpdatePermissionRequestValidator()
    {
        RuleFor(x => x.PermissionName)
            .Length(2, 100).WithMessage("Tên quyền phải từ 2-100 ký tự")
            .Matches(@"^[a-zA-Z0-9\.\-_]+$").WithMessage("Tên quyền chỉ được chứa chữ cái, số, dấu chấm, gạch ngang và gạch dưới")
            .When(x => !string.IsNullOrEmpty(x.PermissionName));

        RuleFor(x => x.Description)
            .MaximumLength(300).WithMessage("Mô tả không được vượt quá 300 ký tự")
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}
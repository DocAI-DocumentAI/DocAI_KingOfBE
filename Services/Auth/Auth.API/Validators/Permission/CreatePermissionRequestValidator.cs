using FluentValidation;
using Auth.API.Payload.Request.Permission;
using Auth.API.Services.Interface;

namespace Auth.API.Validators.Permission;

public class CreatePermissionRequestValidator : AbstractValidator<CreatePermissionRequest>
{
    private readonly IValidationService _validationService;

    public CreatePermissionRequestValidator(IValidationService validationService)
    {
        _validationService = validationService;

        RuleFor(x => x.PermissonName)
            .NotEmpty().WithMessage("Tên quyền không được để trống")
            .Length(2, 100).WithMessage("Tên quyền phải từ 2-100 ký tự")
            .Matches(@"^[a-zA-Z0-9\.\-_]+$").WithMessage("Tên quyền chỉ được chứa chữ cái, số, dấu chấm, gạch ngang và gạch dưới")
            .MustAsync(async (name, cancellation) => !await _validationService.IsPermissionNameExistsAsync(name))
            .WithMessage("Tên quyền đã tồn tại");

        RuleFor(x => x.Description)
            .MaximumLength(300).WithMessage("Mô tả không được vượt quá 300 ký tự");
    }
}

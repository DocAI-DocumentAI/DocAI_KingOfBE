using FluentValidation;
using Auth.API.Payload.Request.Department;
using Auth.API.Services.Interface;

namespace Auth.API.Validators.Department;

public class CreateDepartmentRequestValidator : AbstractValidator<CreateDepartmentRequest>
{
    private readonly IValidationService _validationService;

    public CreateDepartmentRequestValidator(IValidationService validationService)
    {
        _validationService = validationService;

        RuleFor(x => x.DepartmentName)
            .NotEmpty().WithMessage("Tên phòng ban không được để trống")
            .Length(2, 100).WithMessage("Tên phòng ban phải từ 2-100 ký tự")
            .Matches(@"^[a-zA-ZÀ-ỹ0-9\s\-_]+$").WithMessage("Tên phòng ban chỉ được chứa chữ cái, số, khoảng trắng, dấu gạch ngang và gạch dưới")
            .MustAsync(async (name, cancellation) => !await _validationService.IsDepartmentNameExistsAsync(name))
            .WithMessage("Tên phòng ban đã tồn tại");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Mô tả không được vượt quá 500 ký tự");
    }
}

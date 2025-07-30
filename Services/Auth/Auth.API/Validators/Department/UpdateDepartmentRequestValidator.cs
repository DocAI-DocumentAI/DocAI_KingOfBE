using FluentValidation;
using Auth.API.Payload.Request.Department;

namespace Auth.API.Validators.Department;

public class UpdateDepartmentRequestValidator : AbstractValidator<UpdateDepartmentRequest>
{
    public UpdateDepartmentRequestValidator()
    {
        RuleFor(x => x.DepartmentName)
            .Length(2, 100).WithMessage("Tên phòng ban phải từ 2-100 ký tự")
            .Matches(@"^[a-zA-ZÀ-ỹ0-9\s\-_]+$").WithMessage("Tên phòng ban chỉ được chứa chữ cái, số, khoảng trắng, dấu gạch ngang và gạch dưới")
            .When(x => !string.IsNullOrEmpty(x.DepartmentName));

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Mô tả không được vượt quá 500 ký tự")
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}
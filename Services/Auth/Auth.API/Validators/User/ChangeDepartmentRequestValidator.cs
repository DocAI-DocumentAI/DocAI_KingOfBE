using FluentValidation;
using Auth.API.Payload.Request.User;

namespace Auth.API.Validators.User;

public class ChangeDepartmentRequestValidator : AbstractValidator<ChangeDepartmentRequest>
{
    public ChangeDepartmentRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("ID người dùng không được để trống");

        RuleFor(x => x.DepartmentId)
            .NotEmpty().WithMessage("ID phòng ban mới không được để trống");
    }
}
using FluentValidation;
using Auth.API.Payload.Request.User;
using Auth.API.Services.Interface;
using Auth.API.Payload.Request;
using Auth.Domain.Models;
using Auth.Infrastructure.Repository.Interfaces;

namespace Auth.API.Validators.User;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    private readonly IUnitOfWork<DocAIAuthContext> _unitOfWork;

    public RegisterRequestValidator(IUnitOfWork<DocAIAuthContext> unitOfWork)
    {
        _unitOfWork = unitOfWork;

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống")
            .EmailAddress().WithMessage("Email không đúng định dạng")
            .MaximumLength(255).WithMessage("Email không được vượt quá 255 ký tự")
            .Must(IsEmailUnique)
            .WithMessage("Email đã được sử dụng");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống")
            .MinimumLength(8).WithMessage("Mật khẩu phải có ít nhất 8 ký tự")
            .MaximumLength(128).WithMessage("Mật khẩu không được vượt quá 128 ký tự")
            .Matches(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$")
            .WithMessage("Mật khẩu phải chứa ít nhất 1 chữ thường, 1 chữ hoa, 1 số và 1 ký tự đặc biệt");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên không được để trống")
            .Length(2, 100).WithMessage("Họ tên phải từ 2-100 ký tự")
            .Matches(@"^[a-zA-ZÀ-ỹ\s]+$").WithMessage("Họ tên chỉ được chứa chữ cái và khoảng trắng");

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Số điện thoại không được để trống")
            .Matches(@"^(0[3|5|7|8|9])+([0-9]{8})$").WithMessage("Số điện thoại không đúng định dạng Việt Nam")
            .Must(IsPhoneUnique)
            .WithMessage("Số điện thoại đã được sử dụng");

        RuleFor(x => x.RoleId)
            .NotEmpty().WithMessage("Vai trò không được để trống");

        RuleFor(x => x.DepartmentId)
            .NotEmpty().WithMessage("Phòng ban không được để trống");
    }

    private bool IsEmailUnique(string email)
    {
        var user = _unitOfWork.GetRepository<Domain.Models.User>().GetQuery()
            .Where(u => u.Email.ToLower() == email.ToLower())
            .FirstOrDefault();
        return user == null;
    }

    private bool IsPhoneUnique(string phone)
    {
        var user = _unitOfWork.GetRepository<Domain.Models.User>().GetQuery()
            .Where(u => u.Phone == phone)
            .FirstOrDefault();
        return user == null;
    }
}
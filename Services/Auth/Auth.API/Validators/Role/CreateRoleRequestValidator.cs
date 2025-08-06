using FluentValidation;
using Auth.API.Payload.Request.Role;
using Auth.Domain.Models;
using Auth.Infrastructure.Repository.Interfaces;

namespace Auth.API.Validators.Role;

public class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    private readonly IUnitOfWork<DocAIAuthContext> _unitOfWork;

    public CreateRoleRequestValidator(IUnitOfWork<DocAIAuthContext> unitOfWork)
    {
        _unitOfWork = unitOfWork;

        RuleFor(x => x.RoleName)
            .NotEmpty().WithMessage("Tên vai trò không được để trống")
            .Length(2, 50).WithMessage("Tên vai trò phải từ 2-50 ký tự")
            .Matches(@"^[a-zA-ZÀ-ỹ0-9\s\-_]+$").WithMessage("Tên vai trò chỉ được chứa chữ cái, số, khoảng trắng, dấu gạch ngang và gạch dưới")
            .Must(IsRoleNameUnique)
            .WithMessage("Tên vai trò đã tồn tại");

        RuleFor(x => x.Description)
            .MaximumLength(300).WithMessage("Mô tả không được vượt quá 300 ký tự");
    }

    private bool IsRoleNameUnique(string roleName)
    {
        var role = _unitOfWork.GetRepository<Domain.Models.Role>().GetQuery()
            .Where(r => r.RoleName.ToLower() == roleName.ToLower())
            .FirstOrDefault();
        return role == null;
    }
}

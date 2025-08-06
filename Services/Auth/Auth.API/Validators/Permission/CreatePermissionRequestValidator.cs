using FluentValidation;
using Auth.API.Payload.Request.Permission;
using Auth.Domain.Models;
using Auth.Infrastructure.Repository.Interfaces;

namespace Auth.API.Validators.Permission;

public class CreatePermissionRequestValidator : AbstractValidator<CreatePermissionRequest>
{
    private readonly IUnitOfWork<DocAIAuthContext> _unitOfWork;

    public CreatePermissionRequestValidator(IUnitOfWork<DocAIAuthContext> unitOfWork)
    {
        _unitOfWork = unitOfWork;

        RuleFor(x => x.PermissionName)
            .NotEmpty().WithMessage("Tên quyền không được để trống")
            .Length(2, 50).WithMessage("Tên quyền phải từ 2-50 ký tự")
            .Matches(@"^[a-zA-ZÀ-ỹ0-9\s\-_]+$").WithMessage("Tên quyền chỉ được chứa chữ cái, số, khoảng trắng, dấu gạch ngang và gạch dưới")
            .Must(IsPermissionNameUnique)
            .WithMessage("Tên quyền đã tồn tại");

        RuleFor(x => x.Description)
            .MaximumLength(300).WithMessage("Mô tả không được vượt quá 300 ký tự");
    }

    private bool IsPermissionNameUnique(string permissionName)
    {
        var permission = _unitOfWork.GetRepository<Domain.Models.Permission>().GetQuery()
            .Where(p => p.Name.ToLower() == permissionName.ToLower())
            .FirstOrDefault();
        return permission == null;
    }
}

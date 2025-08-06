using FluentValidation;
using Auth.API.Payload.Request.Department;
using Auth.Domain.Models;
using Auth.Infrastructure.Repository.Interfaces;

namespace Auth.API.Validators.Department;

public class CreateDepartmentRequestValidator : AbstractValidator<CreateDepartmentRequest>
{
    private readonly IUnitOfWork<DocAIAuthContext> _unitOfWork;

    public CreateDepartmentRequestValidator(IUnitOfWork<DocAIAuthContext> unitOfWork)
    {
        _unitOfWork = unitOfWork;

        RuleFor(x => x.DepartmentName)
            .NotEmpty().WithMessage("Tên phòng ban không được để trống")
            .Length(2, 100).WithMessage("Tên phòng ban phải từ 2-100 ký tự")
            .Matches(@"^[a-zA-ZÀ-ỹ0-9\s\-_]+$").WithMessage("Tên phòng ban chỉ được chứa chữ cái, số, khoảng trắng, dấu gạch ngang và gạch dưới")
            .Must(IsDepartmentNameUnique)
            .WithMessage("Tên phòng ban đã tồn tại");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Mô tả không được vượt quá 500 ký tự");
    }

    private bool IsDepartmentNameUnique(string departmentName)
    {
        var department = _unitOfWork.GetRepository<Domain.Models.Department>().GetQuery()
            .Where(d => d.Name.ToLower() == departmentName.ToLower())
            .FirstOrDefault();
        return department == null;
    }
}

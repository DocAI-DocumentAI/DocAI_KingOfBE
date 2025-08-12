using FluentValidation;
using Document.Infrastructure.Filter;
using Document.API.Constants;
using Document.Domain.Enums;

namespace Document.API.Validators;

/// <summary>
/// Validator for ApprovalQueueFilter following project validation patterns
/// </summary>
public class ApprovalQueueFilterValidator : AbstractValidator<ApprovalQueueFilter>
{
    public ApprovalQueueFilterValidator()
    {
        // SubmittedBy validation
        RuleFor(x => x.SubmittedBy)
            .MaximumLength(ValidationConstants.ApprovalQueueFilterSubmittedByMaxLength)
            .WithMessage(string.Format(ValidationMessageConstant.Document.SubmittedByMaxLength, ValidationConstants.ApprovalQueueFilterSubmittedByMaxLength))
            .Matches(@"^[a-zA-Z0-9\-_]+$")
            .WithMessage(ValidationMessageConstant.Document.SubmittedByInvalidCharacters)
            .When(x => !string.IsNullOrEmpty(x.SubmittedBy));

        // ReviewedBy validation
        RuleFor(x => x.ReviewedBy)
            .MaximumLength(ValidationConstants.ApprovalQueueFilterReviewedByMaxLength)
            .WithMessage(string.Format(ValidationMessageConstant.Document.ReviewedByMaxLength, ValidationConstants.ApprovalQueueFilterReviewedByMaxLength))
            .Matches(@"^[a-zA-Z0-9\-_]+$")
            .WithMessage(ValidationMessageConstant.Document.ReviewedByInvalidCharacters)
            .When(x => !string.IsNullOrEmpty(x.ReviewedBy));

        // DocumentTypeId validation
        RuleFor(x => x.DocumentTypeId)
            .MaximumLength(ValidationConstants.ApprovalQueueFilterDocumentTypeIdMaxLength)
            .WithMessage(string.Format(ValidationMessageConstant.Document.DocumentTypeIdMaxLength, ValidationConstants.ApprovalQueueFilterDocumentTypeIdMaxLength))
            .Matches(@"^[a-zA-Z0-9\-_]+$")
            .WithMessage(ValidationMessageConstant.Document.DocumentTypeIdInvalidCharacters)
            .When(x => !string.IsNullOrEmpty(x.DocumentTypeId));

        // Title validation
        RuleFor(x => x.Title)
            .MaximumLength(ValidationConstants.ApprovalQueueFilterTitleMaxLength)
            .WithMessage(string.Format(ValidationMessageConstant.Document.FilterTitleMaxLength, ValidationConstants.ApprovalQueueFilterTitleMaxLength))
            .Matches(ValidationConstants.VietnameseTextRegex)
            .WithMessage(ValidationMessageConstant.Document.FilterTitleInvalidCharacters)
            .When(x => !string.IsNullOrEmpty(x.Title));



        // Date range validation
        RuleFor(x => x)
            .Must(x => !x.FromDate.HasValue || !x.ToDate.HasValue || x.FromDate <= x.ToDate)
            .WithMessage(ValidationMessageConstant.Document.InvalidDateRange);

        // Status validation
        RuleFor(x => x.Status)
            .Must(BeValidStatus)
            .WithMessage(ValidationMessageConstant.Document.InvalidStatus)
            .When(x => x.Status.HasValue);
    }

    /// <summary>
    /// Validates that the status is a valid enum value
    /// </summary>
    private bool BeValidStatus(StatusEnum? status)
    {
        if (!status.HasValue) return true;
        
        return Enum.IsDefined(typeof(StatusEnum), status.Value);
    }
}

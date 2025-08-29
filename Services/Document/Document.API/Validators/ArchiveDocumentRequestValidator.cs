using FluentValidation;
using Document.API.Payload.Request;

namespace Document.API.Validators
{
    public class ArchiveDocumentRequestValidator : AbstractValidator<ArchiveDocumentRequest>
    {
        public ArchiveDocumentRequestValidator()
        {
            RuleFor(x => x.Reason)
                .NotEmpty()
                .WithMessage("Reason for archiving is required.")
                .MinimumLength(10)
                .WithMessage("Archive reason must be at least 10 characters long.")
                .MaximumLength(500)
                .WithMessage("Archive reason must not exceed 500 characters.");

            RuleFor(x => x.Comments)
                .MaximumLength(1000)
                .WithMessage("Comments must not exceed 1000 characters.")
                .When(x => !string.IsNullOrEmpty(x.Comments));
        }
    }
}

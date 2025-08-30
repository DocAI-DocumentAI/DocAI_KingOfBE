using FluentValidation;
using Document.API.Payload.Request;

namespace Document.API.Validators
{
    public class DeleteArchivedDocumentRequestValidator : AbstractValidator<DeleteArchivedDocumentRequest>
    {
        public DeleteArchivedDocumentRequestValidator()
        {
            RuleFor(x => x.ConfirmPermanentDeletion)
                .Must(confirm => confirm == true)
                .WithMessage("You must confirm permanent deletion by setting ConfirmPermanentDeletion to true.");

            RuleFor(x => x.Reason)
                .NotEmpty()
                .WithMessage("Reason for deleting archived document is required.")
                .MinimumLength(10)
                .WithMessage("Delete reason must be at least 10 characters long.")
                .MaximumLength(500)
                .WithMessage("Delete reason must not exceed 500 characters.");
        }
    }
}

using FluentValidation;
using Document.API.Payload.Request;
using Document.API.Constants;

namespace Document.API.Validators;

public class CreateDraftRequestValidator : AbstractValidator<CreateDraftRequest>
{
    public CreateDraftRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage(ValidationMessageConstant.Document.TitleRequired)
            .MaximumLength(ValidationConstants.DocumentTitleMaxLength)
            .WithMessage(string.Format(ValidationMessageConstant.Document.TitleMaxLength, ValidationConstants.DocumentTitleMaxLength))
            .Matches(ValidationConstants.VietnameseTextRegex)
            .WithMessage(ValidationMessageConstant.Document.TitleInvalidCharacters);

        RuleFor(x => x.VersionName)
            .NotEmpty().WithMessage(ValidationMessageConstant.Document.VersionNameRequired)
            .MaximumLength(ValidationConstants.DocumentVersionNameMaxLength)
            .WithMessage(string.Format(ValidationMessageConstant.Document.VersionNameMaxLength, ValidationConstants.DocumentVersionNameMaxLength))
            .Matches(ValidationConstants.VersionNameRegex)
            .WithMessage(ValidationMessageConstant.Document.VersionNameInvalidCharacters);

        RuleFor(x => x.Summary)
            .MaximumLength(ValidationConstants.DocumentSummaryMaxLength)
            .WithMessage(string.Format(ValidationMessageConstant.Document.SummaryMaxLength, ValidationConstants.DocumentSummaryMaxLength))
            // .Matches(ValidationConstants.VietnameseTextRegex)
            // .WithMessage(ValidationMessageConstant.Document.SummaryInvalidCharacters)
            .When(x => !string.IsNullOrEmpty(x.Summary));

        RuleFor(x => x.SignedBy)
            .MaximumLength(ValidationConstants.DocumentSignedByMaxLength)
            .WithMessage(string.Format(ValidationMessageConstant.Document.SignedByMaxLength, ValidationConstants.DocumentSignedByMaxLength))
            .Matches(ValidationConstants.VietnameseNameRegex)
            .WithMessage(ValidationMessageConstant.Document.SignedByInvalidCharacters)
            .When(x => !string.IsNullOrEmpty(x.SignedBy));

        RuleFor(x => x.Description)
            .MaximumLength(ValidationConstants.DocumentDescriptionMaxLength)
            .WithMessage(string.Format(ValidationMessageConstant.Document.DescriptionMaxLength, ValidationConstants.DocumentDescriptionMaxLength))
            .Matches(ValidationConstants.VietnameseTextRegex)
            .WithMessage(ValidationMessageConstant.Document.DescriptionInvalidCharacters)
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.EffectiveFrom)
            .LessThan(x => x.EffectiveUntil)
            .WithMessage(ValidationMessageConstant.Document.EffectiveDateRangeInvalid)
            .When(x => x.EffectiveFrom.HasValue && x.EffectiveUntil.HasValue);

        // RuleFor(x => x.Tags)
        //     .Must(tags => tags == null || tags.Count <= ValidationConstants.DocumentTagsMaxCount)
        //     .WithMessage(string.Format(ValidationMessageConstant.Document.TagsMaxCount, ValidationConstants.DocumentTagsMaxCount));

        // RuleForEach(x => x.Tags)
        //     .MaximumLength(ValidationConstants.DocumentTagMaxLength)
        //     .WithMessage(string.Format(ValidationMessageConstant.Document.TagMaxLength, ValidationConstants.DocumentTagMaxLength))
        //     // .Matches(ValidationConstants.TagRegex)
        //     // .WithMessage(ValidationMessageConstant.Document.TagInvalidCharacters)
        //     .When(x => x.Tags != null);

        RuleFor(x => x.File)
            .NotNull().WithMessage(ValidationMessageConstant.Document.FileRequired)
            .Must(file => file != null && PolicyConstant.SupportedFileTypes.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()))
            .WithMessage(string.Format(ValidationMessageConstant.Document.FileTypeNotSupported, string.Join(", ", PolicyConstant.SupportedFileTypes)))
            .Must(file => file != null && file.Length <= PolicyConstant.MaxFileSizeMB * 1024 * 1024)
            .WithMessage(string.Format(ValidationMessageConstant.Document.FileSizeExceeded, PolicyConstant.MaxFileSizeMB));

        RuleFor(x => x.DocumentTypeId)
            .NotEmpty().WithMessage(ValidationMessageConstant.Document.DocumentTypeRequired);

        // ✅ NEW FOLDER DESIGN: Encourage folder selection for better organization
        RuleFor(x => x.FolderId)
            .NotEmpty().WithMessage("Target folder selection is recommended for better document organization. Documents without a target folder will remain in drafts after approval.")
            .When(x => string.IsNullOrEmpty(x.FolderId))
            .WithSeverity(Severity.Warning); // This is a warning, not an error
    }
}

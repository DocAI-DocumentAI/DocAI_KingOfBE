using FluentValidation;
using Document.API.Payload.Request;
using Document.API.Constants;

namespace Document.API.Validators;

/// <summary>
/// Validator for official documents filter requests following project validation patterns
/// </summary>
public class OfficialDocumentsFilterRequestValidator : AbstractValidator<OfficialDocumentsFilterRequest>
{
    public OfficialDocumentsFilterRequestValidator()
    {
        // Title validation
        RuleFor(x => x.Title)
            .MaximumLength(ValidationConstants.OfficialDocumentsFilterTitleMaxLength)
            .WithMessage(string.Format(ValidationMessageConstant.OfficialDocumentsFilter.TitleMaxLength, 
                ValidationConstants.OfficialDocumentsFilterTitleMaxLength))
            .Matches(ValidationConstants.VietnameseTextRegex)
            .WithMessage(ValidationMessageConstant.OfficialDocumentsFilter.TitleInvalidCharacters)
            .When(x => !string.IsNullOrEmpty(x.Title));

        // Keyword validation
        RuleFor(x => x.Keyword)
            .MaximumLength(ValidationConstants.OfficialDocumentsFilterKeywordMaxLength)
            .WithMessage(string.Format(ValidationMessageConstant.OfficialDocumentsFilter.KeywordMaxLength, 
                ValidationConstants.OfficialDocumentsFilterKeywordMaxLength))
            .Matches(ValidationConstants.VietnameseTextRegex)
            .WithMessage(ValidationMessageConstant.OfficialDocumentsFilter.KeywordInvalidCharacters)
            .When(x => !string.IsNullOrEmpty(x.Keyword));

        // Version name validation
        RuleFor(x => x.VersionName)
            .MaximumLength(ValidationConstants.OfficialDocumentsFilterVersionNameMaxLength)
            .WithMessage(string.Format(ValidationMessageConstant.OfficialDocumentsFilter.VersionNameMaxLength, 
                ValidationConstants.OfficialDocumentsFilterVersionNameMaxLength))
            .Matches(ValidationConstants.VersionNameRegex)
            .WithMessage(ValidationMessageConstant.OfficialDocumentsFilter.VersionNameInvalidCharacters)
            .When(x => !string.IsNullOrEmpty(x.VersionName));

        // Date range validations
        RuleFor(x => x)
            .Must(x => !x.FromDate.HasValue || !x.ToDate.HasValue || x.FromDate.Value <= x.ToDate.Value)
            .WithMessage(ValidationMessageConstant.OfficialDocumentsFilter.InvalidDateRange)
            .WithName("DateRange");

        RuleFor(x => x)
            .Must(x => !x.EffectiveFrom.HasValue || !x.EffectiveUntil.HasValue || x.EffectiveFrom.Value <= x.EffectiveUntil.Value)
            .WithMessage(ValidationMessageConstant.OfficialDocumentsFilter.InvalidEffectiveDateRange)
            .WithName("EffectiveDateRange");

        RuleFor(x => x)
            .Must(x => !x.LastSubmittedFrom.HasValue || !x.LastSubmittedTo.HasValue || x.LastSubmittedFrom.Value <= x.LastSubmittedTo.Value)
            .WithMessage(ValidationMessageConstant.OfficialDocumentsFilter.InvalidSubmittedDateRange)
            .WithName("SubmittedDateRange");

        // Document type validation
        RuleFor(x => x.DocumentTypeId)
            .MaximumLength(ValidationConstants.OfficialDocumentsFilterDocumentTypeIdMaxLength)
            .WithMessage(ValidationMessageConstant.OfficialDocumentsFilter.DocumentTypeIdInvalid)
            .When(x => !string.IsNullOrEmpty(x.DocumentTypeId));

        // Tags validation
        RuleFor(x => x.Tags)
            .Must(tags => tags == null || tags.Count <= ValidationConstants.OfficialDocumentsFilterTagsMaxCount)
            .WithMessage(string.Format(ValidationMessageConstant.OfficialDocumentsFilter.TagsMaxCount, 
                ValidationConstants.OfficialDocumentsFilterTagsMaxCount))
            .When(x => x.Tags != null);

        RuleForEach(x => x.Tags)
            .MaximumLength(ValidationConstants.OfficialDocumentsFilterTagMaxLength)
            .WithMessage(string.Format(ValidationMessageConstant.OfficialDocumentsFilter.TagMaxLength, 
                ValidationConstants.OfficialDocumentsFilterTagMaxLength))
            .Matches(ValidationConstants.TagRegex)
            .WithMessage(ValidationMessageConstant.OfficialDocumentsFilter.TagInvalidCharacters)
            .When(x => x.Tags != null);

        // SignedBy validation
        RuleFor(x => x.SignedBy)
            .MaximumLength(ValidationConstants.OfficialDocumentsFilterSignedByMaxLength)
            .WithMessage(string.Format(ValidationMessageConstant.OfficialDocumentsFilter.SignedByMaxLength, 
                ValidationConstants.OfficialDocumentsFilterSignedByMaxLength))
            .Matches(ValidationConstants.VietnameseTextRegex)
            .WithMessage(ValidationMessageConstant.OfficialDocumentsFilter.SignedByInvalidCharacters)
            .When(x => !string.IsNullOrEmpty(x.SignedBy));

        // File type validation
        RuleFor(x => x.FileType)
            .MaximumLength(ValidationConstants.OfficialDocumentsFilterFileTypeMaxLength)
            .WithMessage(string.Format(ValidationMessageConstant.OfficialDocumentsFilter.FileTypeMaxLength, 
                ValidationConstants.OfficialDocumentsFilterFileTypeMaxLength))
            .Must(BeValidFileType)
            .WithMessage(ValidationMessageConstant.OfficialDocumentsFilter.FileTypeInvalid)
            .When(x => !string.IsNullOrEmpty(x.FileType));

        // File size range validation
        RuleFor(x => x.MinFileSize)
            .GreaterThanOrEqualTo(0)
            .WithMessage(string.Format(ValidationMessageConstant.OfficialDocumentsFilter.FileSizeRange, 0, ValidationConstants.OfficialDocumentsFilterMaxFileSize))
            .When(x => x.MinFileSize.HasValue);

        RuleFor(x => x.MaxFileSize)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(ValidationConstants.OfficialDocumentsFilterMaxFileSize)
            .WithMessage(string.Format(ValidationMessageConstant.OfficialDocumentsFilter.FileSizeRange, 0, ValidationConstants.OfficialDocumentsFilterMaxFileSize))
            .When(x => x.MaxFileSize.HasValue);

        RuleFor(x => x)
            .Must(x => !x.MinFileSize.HasValue || !x.MaxFileSize.HasValue || x.MinFileSize.Value <= x.MaxFileSize.Value)
            .WithMessage(ValidationMessageConstant.OfficialDocumentsFilter.InvalidFileSizeRange)
            .WithName("FileSizeRange");

        // Download count range validation
        RuleFor(x => x.MinDownloads)
            .GreaterThanOrEqualTo(0)
            .WithMessage(string.Format(ValidationMessageConstant.OfficialDocumentsFilter.DownloadCountRange, 0, ValidationConstants.OfficialDocumentsFilterMaxDownloads))
            .When(x => x.MinDownloads.HasValue);

        RuleFor(x => x.MaxDownloads)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(ValidationConstants.OfficialDocumentsFilterMaxDownloads)
            .WithMessage(string.Format(ValidationMessageConstant.OfficialDocumentsFilter.DownloadCountRange, 0, ValidationConstants.OfficialDocumentsFilterMaxDownloads))
            .When(x => x.MaxDownloads.HasValue);

        RuleFor(x => x)
            .Must(x => !x.MinDownloads.HasValue || !x.MaxDownloads.HasValue || x.MinDownloads.Value <= x.MaxDownloads.Value)
            .WithMessage(ValidationMessageConstant.OfficialDocumentsFilter.InvalidDownloadCountRange)
            .WithName("DownloadCountRange");

        // SubmittedBy validation
        RuleFor(x => x.SubmittedBy)
            .MaximumLength(ValidationConstants.OfficialDocumentsFilterSubmittedByMaxLength)
            .WithMessage(ValidationMessageConstant.OfficialDocumentsFilter.SubmittedByInvalid)
            .When(x => !string.IsNullOrEmpty(x.SubmittedBy));
    }

    /// <summary>
    /// Validates if the file type is supported
    /// </summary>
    private static bool BeValidFileType(string? fileType)
    {
        if (string.IsNullOrEmpty(fileType))
            return true;

        var validFileTypes = new[] { "PDF", "DOCX", "DOC", "TXT", "RTF" };
        return validFileTypes.Contains(fileType.ToUpperInvariant());
    }
}

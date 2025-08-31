namespace Document.API.Constants;

public class MessageConstant
{
    public const string ValidationError = "Properties {0} Error: {1}";
    public const string DocumentVersionNotFound = "The specified document version was not found.";
    public const string NotPendingApproval = "This document version is not awaiting approval. Its current status is '{0}'.";
    public const string FileNotAvailableInApprovedFolder = "File not available in approved folder after moving.";
    public const string CommentsRequiredForRejection = "Comments are required to reject a document.";
    public const string DocumentVersionNotFoundDetailed = "Document version not found";
    public const string UnauthorizedToSubmit = "You are not authorized to submit this document for approval";
    public const string CannotSubmitForApproval = "Document version cannot be submitted for approval. Current status: {0}";
    // Azure message commented out for Google Drive migration
    // public const string AzureStorageNotConfigured = "Azure Storage is not configured";
    public const string GoogleDriveNotAvailable = "Google Drive is not available. Please check authentication and configuration.";
    public const string OfficialDocumentVersionNotFound = "Official document version not found.";
    public const string DocumentAlreadyBookmarked = "Document already bookmarked by this user.";
    public const string BookmarkNotFound = "Bookmark not found.";
    public const string MaxDraftsReached = "You have reached the maximum limit of {0} draft documents.";
    public const string DocumentTitleExists = "Document title already exists";
    public const string DocumentVersionNameExists = "Document version name already exists for this title";
    public const string FileAlreadyExists = "This file already exists in the system as '{0}' (Version: {1}, Status: {2}).";
    public const string RejectedFileExists = "You have a rejected document with the same file. Please resubmit or delete the existing one.";
    public const string AnotherUserRejectedFileExists = "Another user has a rejected document with the same file.";
    public const string DraftFileExists = "You already have a draft with the same file.";
    public const string UnauthorizedToEdit = "You do not have permission to edit this document";
    public const string CannotEditWithStatus = "Cannot edit a document with status '{0}'";
    public const string DocumentNotFound = "Document not found.";
    public const string UnauthorizedToDelete = "You do not have permission to delete this document.";
    public const string CanOnlyDeleteDrafts = "Only documents with a 'Draft' and 'Rejected' status can be deleted. The status of this document is '{0}'.";
    public const string UnauthorizedToDeleteApproved = "Only Admins can delete approved or archived documents.";
    public const string CannotDeleteApprovedFromOtherDepartment = "You can only delete approved documents from your own department.";
    public const string DocumentHasActiveReplacements = "Cannot delete document that has active replacement documents in progress.";
    public const string ConfirmDeleteApprovedDocument = "This will permanently delete the approved document and all its data. This action cannot be undone.";
    public const string DraftDocumentNotFound = "Draft document not found.";
    public const string RejectedDocumentNotFound = "Rejected document not found.";
    public const string OfficialDocumentNotFoundForId = "Official document not found for the given document file ID.";
    public const string UnauthorizedToCreateNewVersion = "You do not have permission to create a new version of this document.";
    public const string CanOnlyCreateNewVersionOfApproved = "You can only create a new version of an approved document.";
    public const string TagWithNameExists = "Tag with this name already exists.";
    public const string TagNotFound = "Tag not found.";
    public const string CannotDeleteUsedTag = "Cannot delete tag because it is currently in use by one or more documents.";
    public const string TagNameCannotBeEmpty = "Tag name cannot be empty.";
    public const string UnsupportedFileType = "Unsupported file type. Only PDF and DOCX files are allowed.";
    public const string FileSizeExceeded = "File size exceeds the maximum limit of {0}MB.";
    public const string DepartmentNotAssigned = "Document must be assigned to a department.";
    public const string InvalidEffectiveDates = "'Effective From' date must be before 'Effective Until' date.";
    public const string DocumentAlreadyUnderReplacement = "This document is already in the process of being replaced.";
    public const string UnauthorizedToReplaceDocumentInOtherDepartment = "You do not have permission to replace documents in this department.";
    public const string CanOnlyReplaceApprovedDocument = "Only documents with status 'Approved' can be selected for replacement.";
    public const string CannotReplaceDocumentWithItself = "A document cannot replace itself. Please select a different document for replacement.";
    public const string IneligibleDocumentContent = "Document content is not eligible for upload. Transactional forms or invoices are not allowed.";
    public const string SummaryTooLong = "Generated summary exceeds the maximum allowed length of {0} words.";
    public const string DocumentAlreadyClaimed = "This document is currently being reviewed by {0}.";
    public const string ClaimNotFound = "No active claim found for this document version.";
    public const string UnauthorizedToAccessApprovalQueue = "You do not have permission to view the queue.";
    public const string UnauthorizedToAccessDocument = "You do not have permission to access this document.";

    // DocumentType validation messages
    public const string DocumentTypeNameRequired = "Document type name is required.";
    public const string DocumentTypeNameTooLong = "Document type name must not exceed 100 characters.";
    public const string DocumentTypeDescriptionTooLong = "Document type description must not exceed 500 characters.";
    public const string DocumentTypeNameExists = "Document type with this name already exists.";
    public const string DocumentTypeNotFound = "Document type not found.";
    public const string DocumentTypeInUse = "Cannot delete document type that has associated documents. Please reassign or delete the documents first.";
    public const string DocumentTypeRequired = "Document type is required.";
    public const string InvalidDocumentType = "Invalid document type.";
    public const string UnauthorizedToReleaseClaim = "You are not authorized to release this claim.";
    public const string UnauthorizedToKeepClaimAlive = "You are not authorized to keep this claim alive.";
    public const string InvalidStatusForApprovalQueue = "Invalid status for approval queue list. Only 'Pending' 'Rejected' documents can be accepted.";
    public const string GoogleDriveNotConfigured = "Google Drive is not configured";

    // Archive and Delete messages
    public const string CanOnlyArchiveApprovedDocuments = "Only documents with status 'Approved' can be archived.";
    public const string DocumentAlreadyArchived = "Document is already archived.";
    public const string ArchiveReasonRequired = "Reason for archiving is required.";
    public const string ArchiveReasonTooShort = "Archive reason must be at least 10 characters long.";
    public const string UnauthorizedToArchiveDocument = "You do not have permission to archive this document.";
    public const string CannotArchiveDocumentWithActiveReplacements = "Cannot archive document that has active replacement documents in progress.";
    public const string DocumentArchivedSuccessfully = "Document archived successfully.";
    
    public const string CanOnlyDeleteArchivedDocuments = "Only documents with status 'Archived' can be permanently deleted.";
    public const string DeleteArchivedReasonRequired = "Reason for deleting archived document is required.";
    public const string DeleteArchivedReasonTooShort = "Delete reason must be at least 10 characters long.";
    public const string ConfirmDeleteArchivedDocument = "This will permanently delete the archived document and all its data. This action cannot be undone.";
    public const string UnauthorizedToDeleteArchivedDocument = "You do not have permission to delete this archived document.";
    public const string ArchivedDocumentDeletedSuccessfully = "Archived document deleted successfully.";

    // AI Configuration messages
    public const string AIConfigurationNotFound = "AI configuration not found.";
    public const string AIConfigurationModelNameExists = "AI configuration with this model name already exists.";
    public const string AIConfigurationModelNameRequired = "Model name is required.";
    public const string AIConfigurationModelIdRequired = "Model ID is required.";
    public const string AIConfigurationMaxTokenInvalid = "Max token must be between 1 and 32000.";
    public const string AIConfigurationSystemPromptTooLong = "System prompt must not exceed 10000 characters.";
    public const string AIConfigurationCreatedSuccessfully = "AI configuration created successfully.";
    public const string AIConfigurationUpdatedSuccessfully = "AI configuration updated successfully.";
    public const string AIConfigurationDeletedSuccessfully = "AI configuration deleted successfully.";
    public const string AIConfigurationSetAsDefaultSuccessfully = "AI configuration set as default successfully.";
    public const string AIConfigurationDefaultInitialized = "Default AI configuration initialized successfully.";
    public const string AIConfigurationDefaultAlreadyExists = "Default AI configuration already exists.";
}
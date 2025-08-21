namespace Document.API.Constants
{
    /// <summary>
    /// Comprehensive message constants for folder operations, permissions, and Google Drive synchronization
    /// </summary>
    public static class FolderMessageConstant
    {
        #region Folder Operations

        public static class Operations
        {
            public const string FolderCreatedSuccessfully = "Folder '{0}' created successfully";
            public const string FolderUpdatedSuccessfully = "Folder '{0}' updated successfully";
            public const string FolderDeletedSuccessfully = "Folder '{0}' deleted successfully";
            public const string FolderMovedSuccessfully = "Folder '{0}' moved to '{1}' successfully";
            public const string FolderRenamedSuccessfully = "Folder renamed from '{0}' to '{1}' successfully";
            public const string FolderCopiedSuccessfully = "Folder '{0}' copied to '{1}' successfully";
        }

        #endregion

        #region Validation Errors

        public static class Validation
        {
            public const string InvalidFolderName = "Invalid folder name. Name must be 1-255 characters and cannot contain: \\ / : * ? \" < > |";
            public const string FolderNameTooLong = "Folder name cannot exceed {0} characters";
            public const string FolderNameEmpty = "Folder name cannot be empty";
            public const string FolderNameReserved = "Folder name '{0}' is reserved for system use";
            public const string FolderNameStartsWithUnderscore = "Folder names cannot start with underscore (reserved for system folders)";
            public const string FolderAlreadyExists = "A folder with name '{0}' already exists in this location";
            public const string MaxDepthExceeded = "Maximum folder depth ({0}) exceeded. Cannot create folder at this level";
            public const string CircularReference = "Cannot move folder '{0}' to '{1}' as it would create a circular reference";
            public const string InvalidParentFolder = "Parent folder '{0}' does not exist or is not accessible";
        }

        #endregion

        #region Permission Errors

        public static class Permissions
        {
            public const string InsufficientPermissions = "You do not have sufficient permissions to perform this action on folder '{0}'";
            public const string RequiredPermissionMissing = "This action requires '{0}' permission on folder '{1}'";
            public const string CannotAccessFolder = "You do not have access to folder '{0}'";
            public const string CannotCreateInFolder = "You do not have permission to create folders in '{0}'";
            public const string CannotDeleteFolder = "You do not have permission to delete folder '{0}'";
            public const string CannotMoveFolder = "You do not have permission to move folder '{0}'";
            public const string CannotModifySystemFolder = "System folder '{0}' cannot be modified";
            public const string CannotDeleteSystemFolder = "System folder '{0}' cannot be deleted";
            public const string PermissionGrantedSuccessfully = "Permission '{0}' granted to '{1}' on folder '{2}' successfully";
            public const string PermissionRevokedSuccessfully = "Permission revoked from '{0}' on folder '{1}' successfully";
            public const string PermissionInheritedFromParent = "Permission inherited from parent folder '{0}'";
            public const string PermissionConflictDetected = "Permission conflict detected: {0}";
            public const string AccessDeniedToDepartmentFolders = "Access denied to department folders";
            public const string AccessDeniedToUpdateFolder = "Access denied to update this folder";
            public const string AccessDeniedToMoveToLocation = "Access denied to move folder to this location";
            public const string AccessDeniedToManagePermissions = "Access denied to manage folder permissions";
            public const string AccessDeniedToCreateRootFolders = "Access denied to create root folders in other departments";
            public const string AccessDeniedToFolder = "Access denied to this folder";
            public const string AccessDeniedToSearchInFolder = "Access denied to search in this folder";
            public const string AccessDeniedToMoveFromSourceFolder = "Access denied to move document from source folder";
            public const string AccessDeniedToMoveToTargetFolder = "Access denied to move document to target folder";
            public const string AccessDeniedToUploadToTargetFolder = "Access denied to upload documents to target folder";
        }

        #endregion

        #region Folder Content Errors

        public static class Content
        {
            public const string FolderNotEmpty = "Cannot delete folder '{0}' because it contains {1} item(s)";
            public const string FolderContainsDocuments = "Cannot delete folder '{0}' because it contains {1} document(s)";
            public const string FolderContainsSubfolders = "Cannot delete folder '{0}' because it contains {1} subfolder(s)";
            public const string FolderContainsBoth = "Cannot delete folder '{0}' because it contains {1} document(s) and {2} subfolder(s)";
            public const string MustDeleteContentsFirst = "Please delete all contents before deleting the folder, or use force delete option";
            public const string ForceDeleteWarning = "Force deleting folder '{0}' will permanently delete all {1} contained items";
            public const string DocumentsMovedToParent = "{0} document(s) moved to parent folder '{1}' before deletion";
            public const string SubfoldersMovedToParent = "{0} subfolder(s) moved to parent folder '{1}' before deletion";
        }

        #endregion

        #region Google Drive Sync

        public static class GoogleDriveSync
        {
            public const string SyncStarted = "Starting Google Drive synchronization for folder '{0}'";
            public const string SyncCompleted = "Google Drive synchronization completed for folder '{0}'";
            public const string SyncFailed = "Google Drive synchronization failed for folder '{0}': {1}";
            public const string FolderCreatedInGoogleDrive = "Folder '{0}' created in Google Drive with ID: {1}";
            public const string FolderDeletedFromGoogleDrive = "Folder '{0}' deleted from Google Drive";
            public const string FolderMovedInGoogleDrive = "Folder '{0}' moved in Google Drive from '{1}' to '{2}'";
            public const string PermissionGrantedInGoogleDrive = "Google Drive permission '{0}' granted to '{1}' for folder '{2}'";
            public const string PermissionRevokedInGoogleDrive = "Google Drive permission revoked from '{0}' for folder '{1}'";
            public const string GoogleDriveNotAvailable = "Google Drive is not available. Please check authentication and configuration";
            public const string GoogleDriveQuotaExceeded = "Google Drive storage quota exceeded. Cannot create folder '{0}'";
            public const string GoogleDrivePermissionDenied = "Google Drive permission denied for operation on folder '{0}'";
            public const string GoogleDriveRateLimitExceeded = "Google Drive rate limit exceeded. Operation will be retried automatically";
            public const string FileUploadFailed = "File upload failed: {0}";
            public const string FolderNotFoundInDatabase = "Folder '{0}' not found in database. Functional folders must be created via FolderService first.";
            public const string FailedToDeleteFolder = "Failed to delete folder {0}";
        }

        #endregion

        #region Sync Verification

        public static class SyncVerification
        {
            public const string SyncVerificationStarted = "Starting sync verification between database and Google Drive";
            public const string SyncVerificationCompleted = "Sync verification completed: {0} folders checked, {1} issues found";
            public const string SyncInProgress = "Folder synchronization is in progress. Please wait...";
            public const string SyncUpToDate = "All folders are synchronized between database and Google Drive";
            public const string OrphanedFolderDetected = "Orphaned folder detected: '{0}' exists in {1} but not in {2}";
            public const string MetadataMismatch = "Metadata mismatch detected for folder '{0}': {1}";
            public const string PermissionMismatch = "Permission mismatch detected for folder '{0}': Database has '{1}', Google Drive has '{2}'";
            public const string InvalidGoogleDriveId = "Folder '{0}' has invalid Google Drive ID: '{1}'";
            public const string SyncRepairStarted = "Starting automatic repair of sync issues";
            public const string SyncRepairCompleted = "Sync repair completed: {0} issues repaired, {1} issues remaining";
            public const string SyncRepairFailed = "Sync repair failed for folder '{0}': {1}";
            public const string ManualInterventionRequired = "Manual intervention required for {0} sync issues";
        }

        #endregion

        #region Bulk Operations

        public static class BulkOperations
        {
            public const string BulkOperationStarted = "Starting bulk operation: {0} on {1} folders";
            public const string BulkOperationCompleted = "Bulk operation completed: {0} successful, {1} failed";
            public const string BulkPermissionSetup = "Setting up permissions for {0} users across {1} departments";
            public const string BulkInitializationStarted = "Starting bulk folder initialization for {0} departments";
            public const string BulkInitializationCompleted = "Bulk initialization completed: {0} folders created, {1} permissions set";
            public const string BulkSyncStarted = "Starting bulk synchronization for {0} folders";
            public const string BulkSyncCompleted = "Bulk synchronization completed: {0} folders synchronized, {1} errors";
            public const string PartialSuccess = "Operation partially successful: {0} out of {1} items processed successfully";
        }

        #endregion

        #region System Messages

        public static class System
        {
            public const string FolderNotFound = "Folder with ID '{0}' not found";
            public const string FolderTreeNotFound = "Folder tree for '{0}' not found or not accessible";
            public const string DatabaseConnectionError = "Database connection error during folder operation";
            public const string GoogleDriveConnectionError = "Google Drive connection error during folder operation";
            public const string ConcurrencyConflict = "Folder '{0}' was modified by another user. Please refresh and try again";
            public const string OperationTimeout = "Folder operation timed out after {0} seconds";
            public const string UnexpectedError = "An unexpected error occurred during folder operation: {0}";
            public const string ServiceUnavailable = "Folder service is temporarily unavailable. Please try again later";
            public const string MaintenanceMode = "Folder operations are temporarily disabled for maintenance";
            public const string NoFoldersFoundForDepartment = "No folders found for department {0}";
            public const string NoPublicFoldersFound = "No public folders found";
            public const string TargetFolderNotFound = "Target folder not found";
            public const string PermissionNotFound = "Permission with ID {0} not found";
            public const string ErrorValidatingDepartmentAccess = "Error validating department access";
            public const string FailedToMoveInGoogleDrive = "Failed to move folder in Google Drive";
            public const string ErrorValidatingDepartmentAccessForDocumentMove = "Error validating department access for document move";
            public const string DocumentVersionNotFound = "Document version {0} not found";
            public const string AccessDeniedCannotMoveDocumentsOutsideDepartment = "Access denied: Cannot move documents outside your department. Target folder belongs to '{0}' but you belong to '{1}'. Managers can only move documents within their own department folders.";
        }

        #endregion

        #region Success Messages

        public static class Success
        {
            public const string FolderHierarchyCreated = "Folder hierarchy created successfully: {0} folders, {1} levels deep";
            public const string PermissionsInherited = "Permissions successfully inherited from parent folder '{0}'";
            public const string PermissionsPropagated = "Permissions successfully propagated to {0} subfolders";
            public const string FolderStructureInitialized = "Folder structure initialized for department '{0}': {1} folders created";
            public const string SyncCompleted = "Synchronization completed successfully: All folders are in sync";
            public const string BackupCreated = "Backup created for folder '{0}' before operation";
            public const string CacheRefreshed = "Folder cache refreshed successfully";
        }

        #endregion

        #region Warning Messages

        public static class Warnings
        {
            public const string FolderWillBeDeleted = "Warning: Folder '{0}' and all its contents will be permanently deleted";
            public const string PermissionWillBeRevoked = "Warning: Revoking this permission will remove access for {0} users";
            public const string SyncIssuesDetected = "Warning: {0} sync issues detected between database and Google Drive";
            public const string GoogleDriveSlowResponse = "Warning: Google Drive is responding slowly. Operations may take longer than usual";
            public const string LargeOperationWarning = "Warning: This operation affects {0} folders and may take several minutes";
            public const string PermissionConflictWarning = "Warning: Permission conflict detected. Some users may lose access";
        }

        #endregion

        #region Information Messages

        public static class Information
        {
            public const string FolderCacheUpdated = "Folder cache updated for '{0}'";
            public const string PermissionCheckCompleted = "Permission check completed for user '{0}' on folder '{1}': {2}";
            public const string SyncStatusChecked = "Sync status checked: {0} folders in sync, {1} pending synchronization";
            public const string FolderStatistics = "Folder '{0}' contains {1} documents and {2} subfolders";
            public const string PermissionStatistics = "Folder '{0}' has {1} direct permissions and {2} inherited permissions";
            public const string GoogleDriveQuotaStatus = "Google Drive quota: {0} used of {1} available";
        }

        #endregion
    }
}

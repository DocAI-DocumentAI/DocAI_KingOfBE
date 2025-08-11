using Shared.Commands;

namespace Document.API.Services.Interfaces
{
    /// <summary>
    /// Service for managing Google Drive permission setup for departments and users
    /// Handles automatic permission assignment based on department membership
    /// </summary>
    public interface IGoogleDrivePermissionSetupService
    {
        /// <summary>
        /// Setup Google Drive permissions for a new department
        /// Creates department folders and grants access to all department users
        /// </summary>
        /// <param name="departmentId">Department ID</param>
        /// <param name="departmentName">Department name for logging</param>
        /// <param name="userEmails">List of user emails in the department</param>
        /// <returns>Setup result with success/failure details</returns>
        Task<GoogleDrivePermissionSetupResponse> SetupDepartmentPermissionsAsync(
            string departmentId, 
            string departmentName, 
            List<string> userEmails);

        /// <summary>
        /// Setup Google Drive permissions for a new user
        /// Grants access to their department folders and public folders
        /// </summary>
        /// <param name="userEmail">User email</param>
        /// <param name="departmentId">User's department ID</param>
        /// <param name="departmentName">Department name for logging</param>
        /// <returns>Setup result with success/failure details</returns>
        Task<GoogleDrivePermissionSetupResponse> SetupUserPermissionsAsync(
            string userEmail, 
            string departmentId, 
            string departmentName);

        /// <summary>
        /// Perform initial bulk setup of Google Drive permissions for all existing departments and users
        /// Creates all necessary folder structures and assigns permissions
        /// </summary>
        /// <param name="forceRecreate">Whether to recreate existing folders</param>
        /// <param name="specificDepartmentIds">Optional list of specific departments to setup</param>
        /// <returns>Bulk setup result with comprehensive details</returns>
        Task<GoogleDrivePermissionSetupResponse> InitializeBulkPermissionsAsync(
            bool forceRecreate = false, 
            List<string>? specificDepartmentIds = null);

        /// <summary>
        /// Grant folder access to a user for specific folder types
        /// </summary>
        /// <param name="userEmail">User email</param>
        /// <param name="departmentId">Department ID</param>
        /// <param name="folderTypes">Types of folders to grant access to (approved, archived)</param>
        /// <param name="includePublic">Whether to include public folders</param>
        /// <returns>Success status and error details</returns>
        Task<(bool Success, List<string> Errors)> GrantFolderAccessToUserAsync(
            string userEmail, 
            string departmentId, 
            List<string> folderTypes, 
            bool includePublic = true);

        /// <summary>
        /// Create department-specific folders if they don't exist
        /// </summary>
        /// <param name="departmentId">Department ID</param>
        /// <returns>List of created folder paths</returns>
        Task<List<string>> CreateDepartmentFoldersAsync(string departmentId);

        /// <summary>
        /// Validate Google Drive setup and permissions for a department
        /// </summary>
        /// <param name="departmentId">Department ID to validate</param>
        /// <returns>Validation result with details</returns>
        Task<GoogleDrivePermissionSetupResponse> ValidateDepartmentSetupAsync(string departmentId);
    }
}

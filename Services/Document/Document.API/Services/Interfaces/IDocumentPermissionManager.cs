using Document.Domain.Enums;

namespace Document.API.Services.Interfaces
{
    /// <summary>
    /// Service for managing Google Drive permissions based on document status and user roles
    /// </summary>
    public interface IDocumentPermissionManager
    {
        /// <summary>
        /// Apply appropriate permissions to a document based on its status and properties
        /// </summary>
        /// <param name="fileId">Google Drive file ID</param>
        /// <param name="documentStatus">Current document status</param>
        /// <param name="departmentId">Department ID of the document</param>
        /// <param name="isPublic">Whether the document is public</param>
        /// <param name="ownerId">Document owner ID</param>
        Task ApplyDocumentPermissionsAsync(string fileId, StatusEnum documentStatus, string departmentId, bool isPublic, string ownerId);

        /// <summary>
        /// Update permissions when document status changes (e.g., draft to pending, pending to approved)
        /// </summary>
        /// <param name="fileId">Google Drive file ID</param>
        /// <param name="oldStatus">Previous document status</param>
        /// <param name="newStatus">New document status</param>
        /// <param name="departmentId">Department ID of the document</param>
        /// <param name="isPublic">Whether the document is public</param>
        /// <param name="ownerId">Document owner ID</param>
        Task UpdateDocumentPermissionsAsync(string fileId, StatusEnum oldStatus, StatusEnum newStatus, string departmentId, bool isPublic, string ownerId);

        /// <summary>
        /// Grant read access to all company employees (for public approved documents)
        /// </summary>
        /// <param name="fileId">Google Drive file ID</param>
        Task GrantPublicCompanyAccessAsync(string fileId);

        /// <summary>
        /// Grant read access to all employees in a specific department
        /// </summary>
        /// <param name="fileId">Google Drive file ID</param>
        /// <param name="departmentId">Department ID</param>
        Task GrantDepartmentAccessAsync(string fileId, string departmentId);

        /// <summary>
        /// Grant read access to managers in a specific department (for pending documents)
        /// </summary>
        /// <param name="fileId">Google Drive file ID</param>
        /// <param name="departmentId">Department ID</param>
        Task GrantDepartmentManagerAccessAsync(string fileId, string departmentId);

        /// <summary>
        /// Grant read access to document owner only (for draft documents)
        /// </summary>
        /// <param name="fileId">Google Drive file ID</param>
        /// <param name="ownerId">Document owner ID</param>
        Task GrantOwnerOnlyAccessAsync(string fileId, string ownerId);

        /// <summary>
        /// Remove all user permissions except company account (for cleanup)
        /// </summary>
        /// <param name="fileId">Google Drive file ID</param>
        Task RemoveAllUserPermissionsAsync(string fileId);

        /// <summary>
        /// Get all employees in a department from Auth service
        /// </summary>
        /// <param name="departmentId">Department ID</param>
        /// <returns>List of user emails in the department</returns>
        Task<List<string>> GetDepartmentEmployeeEmailsAsync(string departmentId);

        /// <summary>
        /// Get all managers in a department from Auth service
        /// </summary>
        /// <param name="departmentId">Department ID</param>
        /// <returns>List of manager emails in the department</returns>
        Task<List<string>> GetDepartmentManagerEmailsAsync(string departmentId);

        /// <summary>
        /// Get all company employee emails from Auth service
        /// </summary>
        /// <returns>List of all employee emails</returns>
        Task<List<string>> GetAllCompanyEmployeeEmailsAsync();

        /// <summary>
        /// Get user email by user ID from Auth service
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>User email</returns>
        Task<string> GetUserEmailAsync(string userId);
    }
}

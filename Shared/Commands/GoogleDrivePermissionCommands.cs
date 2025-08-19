namespace Shared.Commands
{
    /// <summary>
    /// Command to setup Google Drive permissions for a new department
    /// </summary>
    public class SetupDepartmentGoogleDrivePermissionsCommand
    {
        public string DepartmentId { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public List<string> UserEmails { get; set; } = new List<string>();
    }

    /// <summary>
    /// Command to setup Google Drive permissions for a new user
    /// </summary>
    public class SetupUserGoogleDrivePermissionsCommand
    {
        public string UserId { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string DepartmentId { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Command to setup database folder permissions for a new user
    /// </summary>
    public class SetupUserFolderPermissionsCommand
    {
        public string UserId { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string DepartmentId { get; set; } = string.Empty;
        public string UserRole { get; set; } = string.Empty;
    }

    /// <summary>
    /// Command to perform initial bulk Google Drive permission setup for all existing departments and users
    /// </summary>
    public class InitializeBulkGoogleDrivePermissionsCommand
    {
        public bool ForceRecreate { get; set; } = false;
        public List<string>? SpecificDepartmentIds { get; set; }
    }

    /// <summary>
    /// Response for Google Drive permission setup operations
    /// </summary>
    public class GoogleDrivePermissionSetupResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int TotalUsers { get; set; }
        public int SuccessfulPermissions { get; set; }
        public int FailedPermissions { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> CreatedFolders { get; set; } = new List<string>();
    }

    /// <summary>
    /// Department information for Google Drive setup
    /// </summary>
    public class DepartmentGoogleDriveInfo
    {
        public string DepartmentId { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public List<UserGoogleDriveInfo> Users { get; set; } = new List<UserGoogleDriveInfo>();
    }

    /// <summary>
    /// User information for Google Drive setup
    /// </summary>
    public class UserGoogleDriveInfo
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string DepartmentId { get; set; } = string.Empty;
    }
}

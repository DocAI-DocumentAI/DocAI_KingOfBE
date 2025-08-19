using Document.Domain.Models;
using Document.Domain.Enums;

namespace Document.API.Constants
{
    /// <summary>
    /// Constants for folder management system
    /// </summary>
    public static class FolderConstant
    {
        /// <summary>
        /// System folder names (prefixed with underscore to indicate they are reserved)
        /// </summary>
        public static class SystemFolders
        {
            public const string Draft = "_draft";
            public const string Pending = "_pending";
            public const string Approved = "_approved";
            public const string Archived = "_archived";
        }

        /// <summary>
        /// Root folder names
        /// </summary>
        public static class RootFolders
        {
            public const string Public = "Public";
            public const string Departments = "Departments";
        }

        /// <summary>
        /// Folder validation constants
        /// </summary>
        public static class Validation
        {
            public const int MaxFolderNameLength = 100;
            public const int MinFolderNameLength = 1;
            public const int MaxDescriptionLength = 500;
            public const int MaxFolderDepth = 10;
            public const int MaxFullPathLength = 1000;
        }

        /// <summary>
        /// Folder naming rules
        /// </summary>
        public static class NamingRules
        {
            public const string SystemFolderPrefix = "_";
            public const string PathSeparator = "/";
            
            /// <summary>
            /// Characters not allowed in folder names
            /// </summary>
            public static readonly char[] InvalidCharacters = { '<', '>', ':', '"', '|', '?', '*', '\\' };
            
            /// <summary>
            /// Reserved folder names that cannot be used
            /// </summary>
            public static readonly string[] ReservedNames = 
            {
                "CON", "PRN", "AUX", "NUL",
                "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
            };
        }

        /// <summary>
        /// Default folder permissions - Simple department-based system
        /// </summary>
        public static class DefaultPermissions
        {
            /// <summary>
            /// Department managers have full control over their department folders
            /// </summary>
            public const PermissionType DepartmentManagerPermission = PermissionType.Manage;

            /// <summary>
            /// ✅ FIXED: Department members have VIEW access by default
            /// Only specific users get Edit permissions (managed by managers)
            /// </summary>
            public const PermissionType DepartmentMemberPermission = PermissionType.View;

            /// <summary>
            /// All employees have view access to public folders
            /// </summary>
            public const PermissionType PublicFolderPermission = PermissionType.View;
        }

        /// <summary>
        /// Folder operation error codes
        /// </summary>
        public static class ErrorCodes
        {
            public const string FolderNotFound = "FOLDER_NOT_FOUND";
            public const string FolderAlreadyExists = "FOLDER_ALREADY_EXISTS";
            public const string InvalidFolderName = "INVALID_FOLDER_NAME";
            public const string MaxDepthExceeded = "MAX_DEPTH_EXCEEDED";
            public const string CannotDeleteSystemFolder = "CANNOT_DELETE_SYSTEM_FOLDER";
            public const string CannotDeleteNonEmptyFolder = "CANNOT_DELETE_NON_EMPTY_FOLDER";
            public const string InsufficientPermissions = "INSUFFICIENT_PERMISSIONS";
            public const string CircularReference = "CIRCULAR_REFERENCE";
            public const string FolderContainsDocuments = "FOLDER_CONTAINS_DOCUMENTS";
            public const string FolderContainsSubfolders = "FOLDER_CONTAINS_SUBFOLDERS";
        }

        /// <summary>
        /// Folder operation messages
        /// </summary>
        public static class Messages
        {
            public const string FolderCreatedSuccessfully = "Folder created successfully";
            public const string FolderUpdatedSuccessfully = "Folder updated successfully";
            public const string FolderDeletedSuccessfully = "Folder deleted successfully";
            public const string FolderMovedSuccessfully = "Folder moved successfully";
            public const string PermissionGrantedSuccessfully = "Permission granted successfully";
            public const string PermissionRevokedSuccessfully = "Permission revoked successfully";
        }

        /// <summary>
        /// Helper methods for folder operations
        /// </summary>
        public static class Helpers
        {
            /// <summary>
            /// Check if a folder name is a system folder
            /// </summary>
            public static bool IsSystemFolder(string folderName)
            {
                return folderName?.StartsWith(NamingRules.SystemFolderPrefix) == true;
            }

            /// <summary>
            /// Get all system folder names
            /// </summary>
            public static string[] GetSystemFolderNames()
            {
                return new[]
                {
                    SystemFolders.Draft,
                    SystemFolders.Pending,
                    SystemFolders.Approved,
                    SystemFolders.Archived
                };
            }

            /// <summary>
            /// Validate folder name according to naming rules
            /// </summary>
            public static bool IsValidFolderName(string folderName)
            {
                if (string.IsNullOrWhiteSpace(folderName))
                    return false;

                if (folderName.Length < Validation.MinFolderNameLength || 
                    folderName.Length > Validation.MaxFolderNameLength)
                    return false;

                if (folderName.IndexOfAny(NamingRules.InvalidCharacters) >= 0)
                    return false;

                if (NamingRules.ReservedNames.Contains(folderName.ToUpperInvariant()))
                    return false;

                return true;
            }

            /// <summary>
            /// Build full path from folder hierarchy
            /// </summary>
            public static string BuildFullPath(string parentPath, string folderName)
            {
                if (string.IsNullOrEmpty(parentPath))
                    return folderName;

                return $"{parentPath}{NamingRules.PathSeparator}{folderName}";
            }

            /// <summary>
            /// Get folder level from full path
            /// </summary>
            public static int GetFolderLevel(string fullPath)
            {
                if (string.IsNullOrEmpty(fullPath))
                    return 0;

                return fullPath.Split(NamingRules.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Length - 1;
            }
        }
    }
}

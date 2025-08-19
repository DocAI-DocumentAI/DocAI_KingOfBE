using System.ComponentModel;

namespace Document.Domain.Enums
{
    /// <summary>
    /// Types of folders in the system
    /// </summary>
    public enum FolderType
    {
        /// <summary>
        /// Regular user-created folder
        /// </summary>
        [Description("Regular")]
        Regular = 0,

        /// <summary>
        /// System folder for draft documents
        /// </summary>
        [Description("Draft")]
        Draft = 1,

        /// <summary>
        /// System folder for pending approval documents
        /// </summary>
        [Description("Pending")]
        Pending = 2,

        /// <summary>
        /// System folder for approved documents
        /// </summary>
        [Description("Approved")]
        Approved = 3,

        /// <summary>
        /// System folder for archived documents
        /// </summary>
        [Description("Archived")]
        Archived = 4,

        /// <summary>
        /// Root folder for department
        /// </summary>
        [Description("Department Root")]
        DepartmentRoot = 5,

        /// <summary>
        /// Root folder for public documents
        /// </summary>
        [Description("Public Root")]
        PublicRoot = 6
    }

    /// <summary>
    /// Extension methods for FolderType
    /// </summary>
    public static class FolderTypeExtensions
    {
        /// <summary>
        /// Get description of the folder type
        /// </summary>
        /// <param name="folderType">Folder type</param>
        /// <returns>Description string</returns>
        public static string GetDescription(this FolderType folderType)
        {
            var field = folderType.GetType().GetField(folderType.ToString());
            var attribute = (DescriptionAttribute?)Attribute.GetCustomAttribute(field!, typeof(DescriptionAttribute));
            return attribute?.Description ?? folderType.ToString();
        }

        /// <summary>
        /// Check if folder type is a system folder
        /// </summary>
        /// <param name="folderType">Folder type</param>
        /// <returns>True if system folder</returns>
        public static bool IsSystemFolder(this FolderType folderType)
        {
            return folderType != FolderType.Regular;
        }

        /// <summary>
        /// Check if folder type is a root folder
        /// </summary>
        /// <param name="folderType">Folder type</param>
        /// <returns>True if root folder</returns>
        public static bool IsRootFolder(this FolderType folderType)
        {
            return folderType == FolderType.DepartmentRoot || folderType == FolderType.PublicRoot;
        }

        /// <summary>
        /// Check if folder type is for document status
        /// </summary>
        /// <param name="folderType">Folder type</param>
        /// <returns>True if status folder</returns>
        public static bool IsStatusFolder(this FolderType folderType)
        {
            return folderType == FolderType.Draft ||
                   folderType == FolderType.Pending ||
                   folderType == FolderType.Approved ||
                   folderType == FolderType.Archived;
        }
    }
}

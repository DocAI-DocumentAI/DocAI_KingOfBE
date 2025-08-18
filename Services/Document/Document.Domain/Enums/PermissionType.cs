using System.ComponentModel;

namespace Document.Domain.Enums
{
    /// <summary>
    /// Permission types for folder access control
    /// Hierarchical permissions where higher levels include lower levels
    /// </summary>
    public enum PermissionType
    {
        /// <summary>
        /// View only access - can see folder and documents
        /// </summary>
        [Description("View")]
        View = 1,

        /// <summary>
        /// Edit access - can view, upload documents, create subfolders
        /// </summary>
        [Description("Edit")]
        Edit = 2,

        /// <summary>
        /// Delete access - can view, edit, and delete documents/folders
        /// </summary>
        [Description("Delete")]
        Delete = 3,

        /// <summary>
        /// Manage access - full control including permission management
        /// </summary>
        [Description("Manage")]
        Manage = 4
    }

    /// <summary>
    /// Extension methods for PermissionType
    /// </summary>
    public static class PermissionTypeExtensions
    {
        /// <summary>
        /// Check if current permission includes the required permission level
        /// </summary>
        /// <param name="current">Current permission level</param>
        /// <param name="required">Required permission level</param>
        /// <returns>True if current permission includes required level</returns>
        public static bool Includes(this PermissionType current, PermissionType required)
        {
            return (int)current >= (int)required;
        }

        /// <summary>
        /// Get description of the permission type
        /// </summary>
        /// <param name="permissionType">Permission type</param>
        /// <returns>Description string</returns>
        public static string GetDescription(this PermissionType permissionType)
        {
            var field = permissionType.GetType().GetField(permissionType.ToString());
            var attribute = (DescriptionAttribute?)Attribute.GetCustomAttribute(field!, typeof(DescriptionAttribute));
            return attribute?.Description ?? permissionType.ToString();
        }

        /// <summary>
        /// Get all permission types that are included in the current permission
        /// </summary>
        /// <param name="current">Current permission level</param>
        /// <returns>List of included permission types</returns>
        public static List<PermissionType> GetIncludedPermissions(this PermissionType current)
        {
            var included = new List<PermissionType>();
            
            foreach (PermissionType permission in Enum.GetValues<PermissionType>())
            {
                if (current.Includes(permission))
                {
                    included.Add(permission);
                }
            }
            
            return included;
        }

        /// <summary>
        /// Check if permission type allows specific action
        /// </summary>
        /// <param name="permission">Permission type</param>
        /// <param name="action">Action to check</param>
        /// <returns>True if action is allowed</returns>
        public static bool AllowsAction(this PermissionType permission, string action)
        {
            return action.ToLower() switch
            {
                "view" or "read" or "list" => permission.Includes(PermissionType.View),
                "create" or "upload" or "add" => permission.Includes(PermissionType.Edit),
                "edit" or "update" or "modify" => permission.Includes(PermissionType.Edit),
                "delete" or "remove" => permission.Includes(PermissionType.Delete),
                "manage" or "admin" or "permissions" => permission.Includes(PermissionType.Manage),
                _ => false
            };
        }
    }
}

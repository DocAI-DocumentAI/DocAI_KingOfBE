namespace Document.API.Constants;

public static class StorageFolderConstant
{
    // Only drafts folder is used for temporary storage
    // Approved documents go directly to functional folders
    [Obsolete("Use FolderConstant.SystemFolders.Draft instead for consistency")]
    public const string Drafts = "_draft"; // Updated to match FolderConstant naming

    // Legacy constants for backward compatibility (deprecated)
    [Obsolete("Use functional folders instead. Documents move directly to target folders when approved.")]
    public const string Pending = "pending";

    [Obsolete("Use functional folders instead. Documents move directly to target folders when approved.")]
    public const string Approved = "approved";

    [Obsolete("Use in-place archiving instead. Documents are archived in their current functional folders.")]
    public const string Archived = "archived";

}
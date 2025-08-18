using Document.API.Payload.Response.Folder;

namespace Document.API.Services.Interfaces;

/// <summary>
/// Service for enriching folder permission responses with user and department names
/// </summary>
public interface IFolderPermissionEnrichmentService
{
    /// <summary>
    /// Enrich a single folder permission response with names
    /// </summary>
    /// <param name="permission">Folder permission response to enrich</param>
    /// <returns>Enriched folder permission response</returns>
    Task<FolderPermissionResponse> EnrichFolderPermissionResponseAsync(FolderPermissionResponse permission);
    
    /// <summary>
    /// Enrich multiple folder permission responses with names (bulk operation for better performance)
    /// </summary>
    /// <param name="permissions">List of folder permission responses to enrich</param>
    /// <returns>List of enriched folder permission responses</returns>
    Task<List<FolderPermissionResponse>> EnrichFolderPermissionResponsesAsync(List<FolderPermissionResponse> permissions);
}

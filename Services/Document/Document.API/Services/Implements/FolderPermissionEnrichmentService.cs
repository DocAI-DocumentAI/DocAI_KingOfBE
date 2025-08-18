using Document.API.Payload.Response.Folder;
using Document.API.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Document.API.Services.Implements;

/// <summary>
/// Service for enriching folder permission responses with user and department names
/// </summary>
public class FolderPermissionEnrichmentService : IFolderPermissionEnrichmentService
{
    private readonly INameLookupService _nameLookupService;
    private readonly IUserEmailLookupService _userEmailLookupService;
    private readonly ILogger<FolderPermissionEnrichmentService> _logger;

    public FolderPermissionEnrichmentService(
        INameLookupService nameLookupService,
        IUserEmailLookupService userEmailLookupService,
        ILogger<FolderPermissionEnrichmentService> logger)
    {
        _nameLookupService = nameLookupService;
        _userEmailLookupService = userEmailLookupService;
        _logger = logger;
    }

    public async Task<FolderPermissionResponse> EnrichFolderPermissionResponseAsync(FolderPermissionResponse permission)
    {
        try
        {
            var permissions = new List<FolderPermissionResponse> { permission };
            var enrichedPermissions = await EnrichFolderPermissionResponsesAsync(permissions);
            return enrichedPermissions.First();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching folder permission response for permission {PermissionId}", permission.Id);
            return permission; // Return original if enrichment fails
        }
    }

    public async Task<List<FolderPermissionResponse>> EnrichFolderPermissionResponsesAsync(List<FolderPermissionResponse> permissions)
    {
        try
        {
            if (!permissions.Any())
            {
                _logger.LogInformation("No folder permissions to enrich");
                return permissions;
            }

            _logger.LogInformation("Enriching {Count} folder permission responses with names", permissions.Count);

            // Collect all unique user IDs and department IDs
            var userIds = new HashSet<string>();
            var departmentIds = new HashSet<string>();

            foreach (var permission in permissions)
            {
                if (!string.IsNullOrEmpty(permission.UserId))
                    userIds.Add(permission.UserId);
                
                if (!string.IsNullOrEmpty(permission.DepartmentId))
                    departmentIds.Add(permission.DepartmentId);
                
                if (!string.IsNullOrEmpty(permission.CreatedBy))
                    userIds.Add(permission.CreatedBy);
            }

            // Bulk lookup names and emails from Auth service
            var nameResponse = await _nameLookupService.GetNamesAsync(
                userIds.ToList(),
                departmentIds.ToList()
            );

            var userEmails = await _userEmailLookupService.GetUserEmailsAsync(userIds.ToList());

            if (nameResponse.Success)
            {
                // Enrich each permission with names and emails
                foreach (var permission in permissions)
                {
                    // Enrich user information
                    if (!string.IsNullOrEmpty(permission.UserId))
                    {
                        if (nameResponse.UserNames.TryGetValue(permission.UserId, out string? userName))
                        {
                            permission.UserFullName = userName;
                        }

                        if (userEmails.TryGetValue(permission.UserId, out string? userEmail))
                        {
                            permission.UserEmail = userEmail;
                        }
                    }

                    // Enrich department information
                    if (!string.IsNullOrEmpty(permission.DepartmentId) &&
                        nameResponse.DepartmentNames.TryGetValue(permission.DepartmentId, out string? deptName))
                    {
                        permission.DepartmentName = deptName;
                    }
                }

                _logger.LogInformation("Successfully enriched {Count} folder permission responses with names and emails", permissions.Count);
            }
            else
            {
                _logger.LogWarning("Failed to enrich folder permission responses with names: {ErrorMessage}",
                    nameResponse.ErrorMessage);
            }

            return permissions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching folder permission responses with names");
            return permissions; // Return original list if enrichment fails
        }
    }
}

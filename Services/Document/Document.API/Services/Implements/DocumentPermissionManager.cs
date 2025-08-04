using Document.API.Services.Interfaces;
using Document.Domain.Enums;
using MassTransit;
using Shared.DTOs;
using Document.API.Configuration;
using Microsoft.Extensions.Options;

namespace Document.API.Services.Implements
{
    /// <summary>
    /// Service for managing Google Drive permissions based on document status and user roles
    /// </summary>
    public class DocumentPermissionManager : IDocumentPermissionManager
    {
        private readonly IGoogleDriveService _googleDriveService;
        private readonly IRedisService _redisService;
        private readonly IRequestClient<DepartmentEmployeeRequest> _departmentEmployeeClient;
        private readonly IRequestClient<CompanyEmployeeRequest> _companyEmployeeClient;
        private readonly IRequestClient<UserEmailRequest> _userEmailClient;
        private readonly GoogleDriveConfiguration _googleDriveConfig;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<DocumentPermissionManager> _logger;

        public DocumentPermissionManager(
            IGoogleDriveService googleDriveService,
            IRedisService redisService,
            IRequestClient<DepartmentEmployeeRequest> departmentEmployeeClient,
            IRequestClient<CompanyEmployeeRequest> companyEmployeeClient,
            IRequestClient<UserEmailRequest> userEmailClient,
            IOptions<GoogleDriveConfiguration> googleDriveConfig,
            IHttpContextAccessor httpContextAccessor,
            ILogger<DocumentPermissionManager> logger)
        {
            _googleDriveService = googleDriveService;
            _redisService = redisService;
            _departmentEmployeeClient = departmentEmployeeClient;
            _companyEmployeeClient = companyEmployeeClient;
            _userEmailClient = userEmailClient;
            _googleDriveConfig = googleDriveConfig.Value;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        /// <summary>
        /// Apply appropriate permissions to a document based on its status and properties
        /// </summary>
        public async Task ApplyDocumentPermissionsAsync(string fileId, StatusEnum documentStatus, string departmentId, bool isPublic, string ownerId)
        {
            _logger.LogInformation("Applying permissions for file {FileId}, status: {Status}, department: {DepartmentId}, public: {IsPublic}, owner: {OwnerId}",
                fileId, documentStatus, departmentId, isPublic, ownerId);

            try
            {
                // First, remove all existing user permissions (keep company account)
                await RemoveAllUserPermissionsAsync(fileId);

                // Apply permissions based on document status
                switch (documentStatus)
                {
                    case StatusEnum.Draft:
                        await GrantOwnerOnlyAccessAsync(fileId, ownerId);
                        break;

                    case StatusEnum.Pending:
                        // Grant access to owner and department managers
                        await GrantOwnerOnlyAccessAsync(fileId, ownerId);
                        await GrantDepartmentManagerAccessAsync(fileId, departmentId);
                        break;

                    case StatusEnum.Approved:
                        if (isPublic)
                        {
                            // Public approved documents: all company employees
                            await GrantPublicCompanyAccessAsync(fileId);
                        }
                        else
                        {
                            // Private approved documents: department employees only
                            await GrantDepartmentAccessAsync(fileId, departmentId);
                        }
                        break;

                    case StatusEnum.Archived:
                        // Same rules as approved documents
                        if (isPublic)
                        {
                            await GrantPublicCompanyAccessAsync(fileId);
                        }
                        else
                        {
                            await GrantDepartmentAccessAsync(fileId, departmentId);
                        }
                        break;

                    default:
                        _logger.LogWarning("Unknown document status {Status} for file {FileId}", documentStatus, fileId);
                        break;
                }

                _logger.LogInformation("Successfully applied permissions for file {FileId} with status {Status}", fileId, documentStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying permissions for file {FileId} with status {Status}", fileId, documentStatus);
                throw;
            }
        }

        /// <summary>
        /// Update permissions when document status changes
        /// </summary>
        public async Task UpdateDocumentPermissionsAsync(string fileId, StatusEnum oldStatus, StatusEnum newStatus, string departmentId, bool isPublic, string ownerId)
        {
            _logger.LogInformation("Updating permissions for file {FileId} from {OldStatus} to {NewStatus}", fileId, oldStatus, newStatus);

            // Simply apply the new permissions (this will remove old ones first)
            await ApplyDocumentPermissionsAsync(fileId, newStatus, departmentId, isPublic, ownerId);
        }

        /// <summary>
        /// Grant read access to all company employees (for public approved documents)
        /// </summary>
        public async Task GrantPublicCompanyAccessAsync(string fileId)
        {
            _logger.LogInformation("Granting public company access to file {FileId}", fileId);

            try
            {
                var allEmployeeEmails = await GetAllCompanyEmployeeEmailsAsync();
                
                foreach (var email in allEmployeeEmails)
                {
                    await _googleDriveService.GrantUserAccessAsync(fileId, email, "reader");
                }

                _logger.LogInformation("Granted access to {Count} company employees for file {FileId}", allEmployeeEmails.Count, fileId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error granting public company access to file {FileId}", fileId);
                throw;
            }
        }

        /// <summary>
        /// Grant read access to all employees in a specific department
        /// </summary>
        public async Task GrantDepartmentAccessAsync(string fileId, string departmentId)
        {
            _logger.LogInformation("Granting department access to file {FileId} for department {DepartmentId}", fileId, departmentId);

            try
            {
                var departmentEmails = await GetDepartmentEmployeeEmailsAsync(departmentId);
                
                foreach (var email in departmentEmails)
                {
                    await _googleDriveService.GrantUserAccessAsync(fileId, email, "reader");
                }

                _logger.LogInformation("Granted access to {Count} department employees for file {FileId}", departmentEmails.Count, fileId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error granting department access to file {FileId} for department {DepartmentId}", fileId, departmentId);
                throw;
            }
        }

        /// <summary>
        /// Grant read access to managers in a specific department (for pending documents)
        /// </summary>
        public async Task GrantDepartmentManagerAccessAsync(string fileId, string departmentId)
        {
            _logger.LogInformation("Granting department manager access to file {FileId} for department {DepartmentId}", fileId, departmentId);

            try
            {
                var managerEmails = await GetDepartmentManagerEmailsAsync(departmentId);
                
                foreach (var email in managerEmails)
                {
                    await _googleDriveService.GrantUserAccessAsync(fileId, email, "reader");
                }

                _logger.LogInformation("Granted access to {Count} department managers for file {FileId}", managerEmails.Count, fileId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error granting department manager access to file {FileId} for department {DepartmentId}", fileId, departmentId);
                throw;
            }
        }

        /// <summary>
        /// Grant read access to document owner only (for draft documents)
        /// </summary>
        public async Task GrantOwnerOnlyAccessAsync(string fileId, string ownerId)
        {
            _logger.LogInformation("Granting owner-only access to file {FileId} for owner {OwnerId}", fileId, ownerId);

            try
            {
                var ownerEmail = await GetUserEmailAsync(ownerId);
                await _googleDriveService.GrantUserAccessAsync(fileId, ownerEmail, "reader");

                _logger.LogInformation("Granted owner access to file {FileId} for user {OwnerEmail}", fileId, ownerEmail);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Cannot grant owner access to file {FileId} for owner {OwnerId}: {ErrorMessage}", fileId, ownerId, ex.Message);
                // Don't throw - allow document creation to continue without permissions
                // The document will still be created but without proper Google Drive permissions
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error granting owner access to file {FileId} for owner {OwnerId}", fileId, ownerId);
                // Don't throw - allow document creation to continue
            }
        }

        /// <summary>
        /// Remove all user permissions except company account (for cleanup)
        /// </summary>
        public async Task RemoveAllUserPermissionsAsync(string fileId)
        {
            _logger.LogInformation("Removing all user permissions for file {FileId}", fileId);

            try
            {
                // Get current permissions
                var permissions = await _googleDriveService.GetFilePermissionsAsync(fileId);
                var companyEmail = _googleDriveConfig.CompanyAccountEmail;

                foreach (var permission in permissions)
                {
                    // Skip company account and owner permissions
                    if (permission.Type == "user" && 
                        permission.EmailAddress != companyEmail && 
                        permission.Role != "owner")
                    {
                        await _googleDriveService.RevokeUserAccessAsync(fileId, permission.EmailAddress);
                    }
                }

                _logger.LogInformation("Removed all user permissions for file {FileId}", fileId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user permissions for file {FileId}", fileId);
                throw;
            }
        }

        /// <summary>
        /// Get all employees in a department from Auth service (with Redis caching)
        /// </summary>
        public async Task<List<string>> GetDepartmentEmployeeEmailsAsync(string departmentId)
        {
            try
            {
                _logger.LogInformation("Getting department employee emails for department {DepartmentId}", departmentId);

                // Try to get from cache first
                var cachedEmails = await _redisService.GetDepartmentEmployeesAsync(departmentId);
                if (cachedEmails != null)
                {
                    _logger.LogDebug("Retrieved department employees from cache for {DepartmentId}", departmentId);
                    return cachedEmails;
                }

                // If not in cache, request from Auth service via RabbitMQ
                var request = new DepartmentEmployeeRequest
                {
                    DepartmentId = departmentId,
                    ManagersOnly = false
                };

                var response = await _departmentEmployeeClient.GetResponse<DepartmentEmployeeResponse>(request, timeout: TimeSpan.FromSeconds(30));

                if (response.Message.Success)
                {
                    var emails = response.Message.EmployeeEmails;

                    // Cache the result for 30 minutes
                    await _redisService.SetDepartmentEmployeesAsync(departmentId, emails, TimeSpan.FromMinutes(30));

                    _logger.LogInformation("Retrieved {Count} employee emails for department {DepartmentId}", emails.Count, departmentId);
                    return emails;
                }
                else
                {
                    _logger.LogWarning("Failed to get department employees from Auth service: {ErrorMessage}", response.Message.ErrorMessage);
                    return new List<string>();
                }
            }
            catch (RequestTimeoutException)
            {
                _logger.LogWarning("Timeout getting department employee emails for department {DepartmentId}", departmentId);
                return new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting department employee emails for department {DepartmentId}", departmentId);
                return new List<string>();
            }
        }

        /// <summary>
        /// Get all managers in a department from Auth service (with Redis caching)
        /// </summary>
        public async Task<List<string>> GetDepartmentManagerEmailsAsync(string departmentId)
        {
            try
            {
                _logger.LogInformation("Getting department manager emails for department {DepartmentId}", departmentId);

                // Try to get from cache first
                var cachedEmails = await _redisService.GetDepartmentManagersAsync(departmentId);
                if (cachedEmails != null)
                {
                    _logger.LogDebug("Retrieved department managers from cache for {DepartmentId}", departmentId);
                    return cachedEmails;
                }

                // If not in cache, request from Auth service via RabbitMQ
                var request = new DepartmentEmployeeRequest
                {
                    DepartmentId = departmentId,
                    ManagersOnly = true
                };

                var response = await _departmentEmployeeClient.GetResponse<DepartmentEmployeeResponse>(request, timeout: TimeSpan.FromSeconds(30));

                if (response.Message.Success)
                {
                    var emails = response.Message.EmployeeEmails;

                    // Cache the result for 30 minutes
                    await _redisService.SetDepartmentManagersAsync(departmentId, emails, TimeSpan.FromMinutes(30));

                    _logger.LogInformation("Retrieved {Count} manager emails for department {DepartmentId}", emails.Count, departmentId);
                    return emails;
                }
                else
                {
                    _logger.LogWarning("Failed to get department managers from Auth service: {ErrorMessage}", response.Message.ErrorMessage);
                    return new List<string>();
                }
            }
            catch (RequestTimeoutException)
            {
                _logger.LogWarning("Timeout getting department manager emails for department {DepartmentId}", departmentId);
                return new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting department manager emails for department {DepartmentId}", departmentId);
                return new List<string>();
            }
        }

        /// <summary>
        /// Get all company employee emails from Auth service (with Redis caching)
        /// </summary>
        public async Task<List<string>> GetAllCompanyEmployeeEmailsAsync()
        {
            try
            {
                _logger.LogInformation("Getting all company employee emails");

                // Try to get from cache first
                var cachedEmails = await _redisService.GetAllCompanyEmployeesAsync();
                if (cachedEmails != null)
                {
                    _logger.LogDebug("Retrieved all company employees from cache");
                    return cachedEmails;
                }

                // If not in cache, request from Auth service via RabbitMQ
                var request = new CompanyEmployeeRequest();

                var response = await _companyEmployeeClient.GetResponse<CompanyEmployeeResponse>(request, timeout: TimeSpan.FromSeconds(30));

                if (response.Message.Success)
                {
                    var emails = response.Message.EmployeeEmails;

                    // Cache the result for 60 minutes (longer since this changes less frequently)
                    await _redisService.SetAllCompanyEmployeesAsync(emails, TimeSpan.FromMinutes(60));

                    _logger.LogInformation("Retrieved {Count} company employee emails", emails.Count);
                    return emails;
                }
                else
                {
                    _logger.LogWarning("Failed to get all company employees from Auth service: {ErrorMessage}", response.Message.ErrorMessage);
                    return new List<string>();
                }
            }
            catch (RequestTimeoutException)
            {
                _logger.LogWarning("Timeout getting all company employee emails");
                return new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all company employee emails");
                return new List<string>();
            }
        }

        /// <summary>
        /// Get user email from JWT token (for current user) or from Auth service (for other users)
        /// </summary>
        public async Task<string> GetUserEmailAsync(string userId)
        {
            try
            {
                _logger.LogInformation("Getting user email for user {UserId}", userId);

                // Validate user ID format
                if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out _))
                {
                    throw new ArgumentException($"Invalid user ID format: {userId}");
                }

                // First, check if this is the current user from JWT token
                var currentUserEmail = GetCurrentUserEmailFromToken();
                var currentUserId = GetCurrentUserIdFromToken();

                if (!string.IsNullOrEmpty(currentUserEmail) && !string.IsNullOrEmpty(currentUserId) &&
                    currentUserId.Equals(userId, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Retrieved current user email from JWT token for {UserId}", userId);

                    // Cache the result for future use
                    await _redisService.SetUserEmailAsync(userId, currentUserEmail, TimeSpan.FromHours(24));
                    return currentUserEmail;
                }

                // Try to get from cache first
                var cachedEmail = await _redisService.GetUserEmailAsync(userId);
                if (!string.IsNullOrEmpty(cachedEmail))
                {
                    _logger.LogDebug("Retrieved user email from cache for {UserId}: {Email}", userId, cachedEmail);
                    return cachedEmail;
                }

                // If not in cache, request from Auth service via RabbitMQ
                _logger.LogInformation("User email not in cache, requesting from Auth service for {UserId}", userId);

                var request = new UserEmailRequest
                {
                    UserId = userId,
                    RequestId = Guid.NewGuid().ToString()
                };

                var response = await _userEmailClient.GetResponse<UserEmailResponse>(request, timeout: TimeSpan.FromSeconds(30));

                if (response.Message.Success && !string.IsNullOrEmpty(response.Message.Email))
                {
                    var email = response.Message.Email;

                    // Cache the result for 24 hours (user emails change rarely)
                    await _redisService.SetUserEmailAsync(userId, email, TimeSpan.FromHours(24));

                    _logger.LogInformation("Retrieved email for user {UserId}: {Email}", userId, email);
                    return email;
                }
                else
                {
                    _logger.LogWarning("Failed to get user email from Auth service: {ErrorMessage}", response.Message.ErrorMessage);
                    throw new InvalidOperationException($"User {userId} not found in Auth service. Cannot grant permissions without valid email address.");
                }
            }
            catch (RequestTimeoutException)
            {
                _logger.LogWarning("Timeout getting user email for user {UserId}", userId);
                throw new InvalidOperationException($"Timeout retrieving email for user {userId}. Cannot grant permissions without valid email address.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user email for user {UserId}", userId);
                throw new InvalidOperationException($"Error retrieving email for user {userId}: {ex.Message}. Cannot grant permissions without valid email address.", ex);
            }
        }

        /// <summary>
        /// Get current user's email from JWT token
        /// </summary>
        private string? GetCurrentUserEmailFromToken()
        {
            try
            {
                var user = _httpContextAccessor?.HttpContext?.User;
                if (user == null)
                {
                    return null;
                }

                var email = user.FindFirst("email")?.Value;
                return email;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting email from JWT token");
                return null;
            }
        }

        /// <summary>
        /// Get current user's ID from JWT token
        /// </summary>
        private string? GetCurrentUserIdFromToken()
        {
            try
            {
                var user = _httpContextAccessor?.HttpContext?.User;
                if (user == null)
                {
                    return null;
                }

                var userId = user.FindFirst("userId")?.Value;
                return userId;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting user ID from JWT token");
                return null;
            }
        }
    }
}

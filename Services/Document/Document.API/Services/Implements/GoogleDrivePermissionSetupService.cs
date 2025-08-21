using Document.API.Services.Interfaces;
using Document.Domain.Models;
using Document.Infrastructure.Repository.Interfaces;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Shared.Commands;
using Shared.DTOs;

namespace Document.API.Services.Implements
{
    /// <summary>
    /// Service for managing Google Drive permission setup for departments and users
    /// Implements automatic permission assignment based on department membership
    /// </summary>
    public class GoogleDrivePermissionSetupService : IGoogleDrivePermissionSetupService
    {
        private readonly IGoogleDriveService _googleDriveService;
        private readonly IRequestClient<DepartmentEmployeeRequest> _departmentEmployeeClient;
        private readonly IRequestClient<CompanyEmployeeRequest> _companyEmployeeClient;
        private readonly IRequestClient<GetAllDepartmentsRequest> _getAllDepartmentsClient;
        private readonly IFolderService _folderService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<GoogleDrivePermissionSetupService> _logger;

        // No functional folders created automatically
        // Managers can create custom folders as needed using folder management APIs
        private readonly string[] _departmentFolderTypes = { }; // Empty - no automatic functional folders
        private readonly string[] _publicFolderTypes = { }; // Empty - no automatic functional folders

        public GoogleDrivePermissionSetupService(
            IGoogleDriveService googleDriveService,
            IRequestClient<DepartmentEmployeeRequest> departmentEmployeeClient,
            IRequestClient<CompanyEmployeeRequest> companyEmployeeClient,
            IRequestClient<GetAllDepartmentsRequest> getAllDepartmentsClient,
            IFolderService folderService,
            IUnitOfWork unitOfWork,
            ILogger<GoogleDrivePermissionSetupService> logger)
        {
            _googleDriveService = googleDriveService;
            _departmentEmployeeClient = departmentEmployeeClient;
            _companyEmployeeClient = companyEmployeeClient;
            _getAllDepartmentsClient = getAllDepartmentsClient;
            _folderService = folderService;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<GoogleDrivePermissionSetupResponse> SetupDepartmentPermissionsAsync(
            string departmentId, 
            string departmentName, 
            List<string> userEmails)
        {
            _logger.LogInformation("Setting up Google Drive permissions for department {DepartmentId} ({DepartmentName}) with {UserCount} users",
                departmentId, departmentName, userEmails.Count);

            var response = new GoogleDrivePermissionSetupResponse
            {
                TotalUsers = userEmails.Count
            };

            try
            {
                // 1. Create department folders in both Google Drive and database
                var createdFolders = await CreateDepartmentFoldersAsync(departmentId);
                response.CreatedFolders.AddRange(createdFolders);

                // 2. Create database folder entries
                try
                {
                    var databaseFolderIds = await _folderService.InitializeDepartmentFoldersAsync(departmentId, departmentName);
                    _logger.LogInformation("Created {Count} database folder entries for department {DepartmentId}", databaseFolderIds.Count, departmentId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create database folder entries for department {DepartmentId}. Google Drive folders created successfully.", departmentId);
                }

                // 3. Grant permissions to all department users
                foreach (var userEmail in userEmails)
                {
                    try
                    {
                        var (success, errors) = await GrantFolderAccessToUserAsync(userEmail, departmentId, _departmentFolderTypes.ToList(), true);
                        if (success)
                        {
                            response.SuccessfulPermissions++;
                        }
                        else
                        {
                            response.FailedPermissions++;
                            response.Errors.AddRange(errors);
                        }
                    }
                    catch (Exception ex)
                    {
                        response.FailedPermissions++;
                        var errorMsg = $"Failed to setup permissions for user {userEmail}: {ex.Message}";
                        response.Errors.Add(errorMsg);
                        _logger.LogWarning(ex, "Failed to setup permissions for user {UserEmail} in department {DepartmentId}", userEmail, departmentId);
                    }
                }

                response.Success = response.FailedPermissions == 0;
                response.Message = response.Success 
                    ? $"Successfully setup permissions for department {departmentName}"
                    : $"Setup completed with {response.FailedPermissions} failures for department {departmentName}";

                _logger.LogInformation("Department permission setup completed for {DepartmentId}: {SuccessCount}/{TotalCount} successful",
                    departmentId, response.SuccessfulPermissions, response.TotalUsers);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting up department permissions for {DepartmentId}", departmentId);
                response.Success = false;
                response.Message = $"Failed to setup department permissions: {ex.Message}";
                response.Errors.Add(ex.Message);
                return response;
            }
        }

        public async Task<GoogleDrivePermissionSetupResponse> SetupUserPermissionsAsync(
            string userEmail,
            string departmentId,
            string departmentName)
        {
            _logger.LogInformation("Setting up Google Drive permissions for user {UserEmail} in department {DepartmentId} ({DepartmentName})",
                userEmail, departmentId, departmentName);

            var response = new GoogleDrivePermissionSetupResponse
            {
                TotalUsers = 1
            };

            try
            {
                // Grant access to actual folders from database instead of hardcoded types
                var (success, errors) = await GrantDatabaseFolderAccessToUserAsync(userEmail, departmentId);

                if (success)
                {
                    response.SuccessfulPermissions = 1;
                    response.Success = true;
                    response.Message = $"Successfully setup Google Drive permissions for user {userEmail}";
                }
                else
                {
                    response.FailedPermissions = 1;
                    response.Success = false;
                    response.Message = $"Failed to setup Google Drive permissions for user {userEmail}";
                    response.Errors.AddRange(errors);
                }

                _logger.LogInformation("User Google Drive permission setup completed for {UserEmail}: Success={Success}",
                    userEmail, response.Success);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting up user Google Drive permissions for {UserEmail}", userEmail);
                response.Success = false;
                response.FailedPermissions = 1;
                response.Message = $"Failed to setup user Google Drive permissions: {ex.Message}";
                response.Errors.Add(ex.Message);
                return response;
            }
        }

        public async Task<GoogleDrivePermissionSetupResponse> InitializeBulkPermissionsAsync(
            bool forceRecreate = false,
            List<string>? specificDepartmentIds = null)
        {
            _logger.LogInformation("Starting bulk Google Drive permission initialization. ForceRecreate={ForceRecreate}, SpecificDepartments={SpecificDepartments}",
                forceRecreate, specificDepartmentIds?.Count ?? 0);

            var response = new GoogleDrivePermissionSetupResponse();

            try
            {
                // 1. Initialize company folder structure
                await _googleDriveService.InitializeCompanyFoldersAsync();

                // 2. Create public folders in both Google Drive and database
                var publicFolders = await CreatePublicFoldersAsync();
                response.CreatedFolders.AddRange(publicFolders);

                // 3. Create database public folder entries
                try
                {
                    var databasePublicFolderIds = await _folderService.InitializePublicFoldersAsync();
                    _logger.LogInformation("Created {Count} database public folder entries", databasePublicFolderIds.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create database public folder entries. Google Drive folders created successfully.");
                }

                // 4. Get all departments or specific ones
                var departments = await GetDepartmentsForSetupAsync(specificDepartmentIds);

                foreach (var department in departments)
                {
                    try
                    {
                        var departmentResult = await SetupDepartmentPermissionsAsync(
                            department.DepartmentId,
                            department.DepartmentName,
                            department.Users.Select(u => u.Email).ToList());

                        response.TotalUsers += departmentResult.TotalUsers;
                        response.SuccessfulPermissions += departmentResult.SuccessfulPermissions;
                        response.FailedPermissions += departmentResult.FailedPermissions;
                        response.CreatedFolders.AddRange(departmentResult.CreatedFolders);
                        response.Errors.AddRange(departmentResult.Errors);
                    }
                    catch (Exception ex)
                    {
                        var errorMsg = $"Failed to setup department {department.DepartmentName}: {ex.Message}";
                        response.Errors.Add(errorMsg);
                        _logger.LogError(ex, "Failed to setup department {DepartmentId} during bulk initialization", department.DepartmentId);
                    }
                }

                response.Success = response.FailedPermissions == 0;
                response.Message = response.Success
                    ? $"Bulk initialization completed successfully for {departments.Count} departments"
                    : $"Bulk initialization completed with {response.FailedPermissions} failures across {departments.Count} departments";

                _logger.LogInformation("Bulk permission initialization completed: {SuccessCount}/{TotalCount} successful permissions across {DepartmentCount} departments",
                    response.SuccessfulPermissions, response.TotalUsers, departments.Count);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bulk permission initialization");
                response.Success = false;
                response.Message = $"Bulk initialization failed: {ex.Message}";
                response.Errors.Add(ex.Message);
                return response;
            }
        }

        /// <summary>
        /// Grant Google Drive access to folders that the user should have access to based on database permissions
        /// </summary>
        /// <param name="userEmail">User email</param>
        /// <param name="departmentId">User's department ID</param>
        /// <returns>Success status and any errors</returns>
        public async Task<(bool Success, List<string> Errors)> GrantDatabaseFolderAccessToUserAsync(
            string userEmail,
            string departmentId)
        {
            var errors = new List<string>();
            var successCount = 0;
            var totalAttempts = 0;

            try
            {
                _logger.LogInformation("Granting Google Drive access to database folders for user {UserEmail} in department {DepartmentId}", userEmail, departmentId);

                // Get folders from database that the user should have access to:
                // 1. Public folders (accessible to all employees)
                // 2. Department folders (accessible to department members)
                var accessibleFolders = await _unitOfWork.GetRepository<Folder>()
                    .GetListAsync(
                        predicate: f => !f.IsDeleted &&
                                       (f.IsPublic || f.DepartmentId == departmentId),
                        include: i => i.Include(f => f.FolderPermissions)
                    );

                _logger.LogInformation("Found {FolderCount} accessible folders for user {UserEmail} in department {DepartmentId}",
                    accessibleFolders.Count, userEmail, departmentId);

                foreach (var folder in accessibleFolders)
                {
                    totalAttempts++;
                    try
                    {
                        // Grant viewer access to the Google Drive folder
                        await _googleDriveService.GrantUserAccessAsync(folder.GoogleDriveFolderId, userEmail, "reader");
                        successCount++;
                        _logger.LogDebug("Granted viewer access to folder '{FolderName}' ({GoogleDriveFolderId}) for user {UserEmail}",
                            folder.Name, folder.GoogleDriveFolderId, userEmail);
                    }
                    catch (Exception ex)
                    {
                        var errorMsg = $"Failed to grant access to folder '{folder.Name}' ({folder.GoogleDriveFolderId}): {ex.Message}";
                        errors.Add(errorMsg);
                        _logger.LogWarning(ex, "Failed to grant access to folder '{FolderName}' ({GoogleDriveFolderId}) for user {UserEmail}",
                            folder.Name, folder.GoogleDriveFolderId, userEmail);
                    }
                }

                var success = totalAttempts == 0 || successCount == totalAttempts;

                _logger.LogInformation("Completed Google Drive folder access setup for user {UserEmail}: {SuccessCount}/{TotalAttempts} successful",
                    userEmail, successCount, totalAttempts);

                return (success, errors);
            }
            catch (Exception ex)
            {
                errors.Add($"Unexpected error granting database folder access: {ex.Message}");
                _logger.LogError(ex, "Unexpected error granting database folder access to user {UserEmail}", userEmail);
                return (false, errors);
            }
        }

        public async Task<(bool Success, List<string> Errors)> GrantFolderAccessToUserAsync(
            string userEmail,
            string departmentId,
            List<string> folderTypes,
            bool includePublic = true)
        {
            var errors = new List<string>();
            var successCount = 0;
            var totalAttempts = 0;

            try
            {
                // Grant access to department-specific folders
                foreach (var folderType in folderTypes)
                {
                    totalAttempts++;
                    try
                    {
                        var folderId = await _googleDriveService.GetOrCreateFolderAsync(folderType, departmentId, false);
                        await _googleDriveService.GrantUserAccessAsync(folderId, userEmail, departmentId, false, "reader");
                        successCount++;
                        _logger.LogDebug("Granted access to {FolderType}/{DepartmentId} folder for user {UserEmail}", folderType, departmentId, userEmail);
                    }
                    catch (Exception ex)
                    {
                        var errorMsg = $"Failed to grant access to {folderType}/{departmentId} folder: {ex.Message}";
                        errors.Add(errorMsg);
                        _logger.LogWarning(ex, "Failed to grant access to {FolderType}/{DepartmentId} folder for user {UserEmail}", folderType, departmentId, userEmail);
                    }
                }

                // Grant access to public folders if requested
                if (includePublic)
                {
                    foreach (var folderType in _publicFolderTypes)
                    {
                        totalAttempts++;
                        try
                        {
                            var folderId = await _googleDriveService.GetOrCreateFolderAsync(folderType, null, true);
                            await _googleDriveService.GrantUserAccessAsync(folderId, userEmail, departmentId, true, "reader");
                            successCount++;
                            _logger.LogDebug("Granted access to public {FolderType} folder for user {UserEmail}", folderType, userEmail);
                        }
                        catch (Exception ex)
                        {
                            var errorMsg = $"Failed to grant access to public {folderType} folder: {ex.Message}";
                            errors.Add(errorMsg);
                            _logger.LogWarning(ex, "Failed to grant access to public {FolderType} folder for user {UserEmail}", folderType, userEmail);
                        }
                    }
                }

                var success = successCount == totalAttempts;
                return (success, errors);
            }
            catch (Exception ex)
            {
                errors.Add($"Unexpected error granting folder access: {ex.Message}");
                _logger.LogError(ex, "Unexpected error granting folder access to user {UserEmail}", userEmail);
                return (false, errors);
            }
        }

        public async Task<List<string>> CreateDepartmentFoldersAsync(string departmentId)
        {
            var createdFolders = new List<string>();

            try
            {
                foreach (var folderType in _departmentFolderTypes)
                {
                    try
                    {
                        var folderId = await _googleDriveService.GetOrCreateFolderAsync(folderType, departmentId, false);
                        var folderPath = $"{folderType}/{departmentId}";
                        createdFolders.Add(folderPath);
                        _logger.LogDebug("Created/verified department folder: {FolderPath}", folderPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to create department folder {FolderType}/{DepartmentId}", folderType, departmentId);
                    }
                }

                return createdFolders;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating department folders for {DepartmentId}", departmentId);
                throw;
            }
        }

        public async Task<GoogleDrivePermissionSetupResponse> ValidateDepartmentSetupAsync(string departmentId)
        {
            _logger.LogInformation("Validating Google Drive setup for department {DepartmentId}", departmentId);

            var response = new GoogleDrivePermissionSetupResponse();

            try
            {
                // Check if department folders exist
                var missingFolders = new List<string>();
                foreach (var folderType in _departmentFolderTypes)
                {
                    try
                    {
                        await _googleDriveService.GetOrCreateFolderAsync(folderType, departmentId, false);
                    }
                    catch (Exception)
                    {
                        missingFolders.Add($"{folderType}/{departmentId}");
                    }
                }

                if (missingFolders.Any())
                {
                    response.Success = false;
                    response.Message = $"Missing folders for department {departmentId}: {string.Join(", ", missingFolders)}";
                    response.Errors.AddRange(missingFolders.Select(f => $"Missing folder: {f}"));
                }
                else
                {
                    response.Success = true;
                    response.Message = $"Department {departmentId} setup is valid";
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating department setup for {DepartmentId}", departmentId);
                response.Success = false;
                response.Message = $"Validation failed: {ex.Message}";
                response.Errors.Add(ex.Message);
                return response;
            }
        }

        /// <summary>
        /// Helper method to create public folders
        /// </summary>
        private async Task<List<string>> CreatePublicFoldersAsync()
        {
            var createdFolders = new List<string>();

            foreach (var folderType in _publicFolderTypes)
            {
                try
                {
                    var folderId = await _googleDriveService.GetOrCreateFolderAsync(folderType, null, true);
                    var folderPath = $"{folderType}/public";
                    createdFolders.Add(folderPath);
                    _logger.LogDebug("Created/verified public folder: {FolderPath}", folderPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create public folder {FolderType}", folderType);
                }
            }

            return createdFolders;
        }

        /// <summary>
        /// Helper method to get departments for setup
        /// </summary>
        private async Task<List<DepartmentGoogleDriveInfo>> GetDepartmentsForSetupAsync(List<string>? specificDepartmentIds)
        {
            try
            {
                if (specificDepartmentIds != null && specificDepartmentIds.Any())
                {
                    // Get specific departments
                    var departments = new List<DepartmentGoogleDriveInfo>();
                    foreach (var departmentId in specificDepartmentIds)
                    {
                        var departmentUsers = await GetDepartmentUsersAsync(departmentId);
                        departments.Add(new DepartmentGoogleDriveInfo
                        {
                            DepartmentId = departmentId,
                            DepartmentName = $"Department {departmentId}",
                            Users = departmentUsers
                        });
                    }
                    return departments;
                }
                else
                {
                    // Get all departments from Auth service with extended timeout
                    var departmentsResponse = await _getAllDepartmentsClient.GetResponse<GetAllDepartmentsResponse>(
                        new GetAllDepartmentsRequest(),
                        timeout: TimeSpan.FromMinutes(2));

                    if (!departmentsResponse.Message.Success || departmentsResponse.Message.Departments == null)
                    {
                        _logger.LogWarning("Failed to get departments from Auth service or no departments found");
                        return new List<DepartmentGoogleDriveInfo>();
                    }

                    var departments = new List<DepartmentGoogleDriveInfo>();

                    // For each department, get its users
                    foreach (var dept in departmentsResponse.Message.Departments)
                    {
                        try
                        {
                            var departmentUsers = await GetDepartmentUsersAsync(dept.Id.ToString());

                            if (departmentUsers.Any())
                            {
                                departments.Add(new DepartmentGoogleDriveInfo
                                {
                                    DepartmentId = dept.Id.ToString(),
                                    DepartmentName = dept.Name,
                                    Users = departmentUsers
                                });

                                _logger.LogDebug("Added department {DepartmentName} with {UserCount} users",
                                    dept.Name, departmentUsers.Count);
                            }
                            else
                            {
                                _logger.LogDebug("Department {DepartmentName} has no users, skipping", dept.Name);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to get users for department {DepartmentId} ({DepartmentName})",
                                dept.Id, dept.Name);
                            // Continue with other departments
                        }
                    }

                    _logger.LogInformation("Retrieved {DepartmentCount} departments with users for Google Drive setup", departments.Count);
                    return departments;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting departments for setup");
                throw;
            }
        }

        /// <summary>
        /// Helper method to get users for a specific department
        /// </summary>
        private async Task<List<UserGoogleDriveInfo>> GetDepartmentUsersAsync(string departmentId)
        {
            try
            {
                var response = await _departmentEmployeeClient.GetResponse<DepartmentEmployeeResponse>(
                    new DepartmentEmployeeRequest { DepartmentId = departmentId },
                    timeout: TimeSpan.FromMinutes(1));

                return response.Message.EmployeeEmails.Select(email => new UserGoogleDriveInfo
                {
                    UserId = Guid.NewGuid().ToString(),
                    Email = email,
                    FullName = email.Split('@')[0],
                    DepartmentId = departmentId
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users for department {DepartmentId}", departmentId);
                throw;
            }
        }
    }
}

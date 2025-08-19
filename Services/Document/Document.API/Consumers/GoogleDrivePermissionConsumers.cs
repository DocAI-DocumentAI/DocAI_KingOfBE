using Document.API.Services.Interfaces;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Commands;

namespace Document.API.Consumers
{
    /// <summary>
    /// Consumer for setting up Google Drive permissions for new departments
    /// </summary>
    public class SetupDepartmentGoogleDrivePermissionsConsumer : IConsumer<SetupDepartmentGoogleDrivePermissionsCommand>
    {
        private readonly IGoogleDrivePermissionSetupService _permissionSetupService;
        private readonly ILogger<SetupDepartmentGoogleDrivePermissionsConsumer> _logger;

        public SetupDepartmentGoogleDrivePermissionsConsumer(
            IGoogleDrivePermissionSetupService permissionSetupService,
            ILogger<SetupDepartmentGoogleDrivePermissionsConsumer> logger)
        {
            _permissionSetupService = permissionSetupService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<SetupDepartmentGoogleDrivePermissionsCommand> context)
        {
            var command = context.Message;

            try
            {
                _logger.LogInformation("Processing Google Drive permission setup for department {DepartmentId} ({DepartmentName}) with {UserCount} users",
                    command.DepartmentId, command.DepartmentName, command.UserEmails.Count);

                var result = await _permissionSetupService.SetupDepartmentPermissionsAsync(
                    command.DepartmentId,
                    command.DepartmentName,
                    command.UserEmails);

                if (result.Success)
                {
                    _logger.LogInformation("Successfully setup Google Drive permissions for department {DepartmentId}: {SuccessCount}/{TotalCount} users",
                        command.DepartmentId, result.SuccessfulPermissions, result.TotalUsers);
                }
                else
                {
                    _logger.LogWarning("Google Drive permission setup completed with errors for department {DepartmentId}: {FailureCount} failures. Errors: {Errors}",
                        command.DepartmentId, result.FailedPermissions, string.Join("; ", result.Errors));
                }

                // Respond with the result
                await context.RespondAsync(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Google Drive permission setup for department {DepartmentId}", command.DepartmentId);
                
                var errorResponse = new GoogleDrivePermissionSetupResponse
                {
                    Success = false,
                    Message = $"Failed to setup department permissions: {ex.Message}",
                    TotalUsers = command.UserEmails.Count,
                    FailedPermissions = command.UserEmails.Count,
                    Errors = { ex.Message }
                };

                await context.RespondAsync(errorResponse);
                throw; // Re-throw to trigger retry mechanism
            }
        }
    }

    /// <summary>
    /// Consumer for setting up Google Drive permissions for new users
    /// </summary>
    public class SetupUserGoogleDrivePermissionsConsumer : IConsumer<SetupUserGoogleDrivePermissionsCommand>
    {
        private readonly IGoogleDrivePermissionSetupService _permissionSetupService;
        private readonly ILogger<SetupUserGoogleDrivePermissionsConsumer> _logger;

        public SetupUserGoogleDrivePermissionsConsumer(
            IGoogleDrivePermissionSetupService permissionSetupService,
            ILogger<SetupUserGoogleDrivePermissionsConsumer> logger)
        {
            _permissionSetupService = permissionSetupService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<SetupUserGoogleDrivePermissionsCommand> context)
        {
            var command = context.Message;

            try
            {
                _logger.LogInformation("Processing Google Drive permission setup for user {UserEmail} in department {DepartmentId} ({DepartmentName})",
                    command.UserEmail, command.DepartmentId, command.DepartmentName);

                var result = await _permissionSetupService.SetupUserPermissionsAsync(
                    command.UserEmail,
                    command.DepartmentId,
                    command.DepartmentName);

                if (result.Success)
                {
                    _logger.LogInformation("Successfully setup Google Drive permissions for user {UserEmail}", command.UserEmail);
                }
                else
                {
                    _logger.LogWarning("Google Drive permission setup failed for user {UserEmail}. Errors: {Errors}",
                        command.UserEmail, string.Join("; ", result.Errors));
                }

                // Respond with the result
                await context.RespondAsync(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Google Drive permission setup for user {UserEmail}", command.UserEmail);
                
                var errorResponse = new GoogleDrivePermissionSetupResponse
                {
                    Success = false,
                    Message = $"Failed to setup user permissions: {ex.Message}",
                    TotalUsers = 1,
                    FailedPermissions = 1,
                    Errors = { ex.Message }
                };

                await context.RespondAsync(errorResponse);
                throw; // Re-throw to trigger retry mechanism
            }
        }
    }

    /// <summary>
    /// Consumer for bulk Google Drive permission initialization
    /// </summary>
    public class InitializeBulkGoogleDrivePermissionsConsumer : IConsumer<InitializeBulkGoogleDrivePermissionsCommand>
    {
        private readonly IGoogleDrivePermissionSetupService _permissionSetupService;
        private readonly ILogger<InitializeBulkGoogleDrivePermissionsConsumer> _logger;

        public InitializeBulkGoogleDrivePermissionsConsumer(
            IGoogleDrivePermissionSetupService permissionSetupService,
            ILogger<InitializeBulkGoogleDrivePermissionsConsumer> logger)
        {
            _permissionSetupService = permissionSetupService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<InitializeBulkGoogleDrivePermissionsCommand> context)
        {
            var command = context.Message;

            try
            {
                _logger.LogInformation("Processing bulk Google Drive permission initialization. ForceRecreate={ForceRecreate}, SpecificDepartments={SpecificDepartments}",
                    command.ForceRecreate, command.SpecificDepartmentIds?.Count ?? 0);

                var result = await _permissionSetupService.InitializeBulkPermissionsAsync(
                    command.ForceRecreate,
                    command.SpecificDepartmentIds);

                if (result.Success)
                {
                    _logger.LogInformation("Successfully completed bulk Google Drive permission initialization: {SuccessCount}/{TotalCount} users across {FolderCount} folders",
                        result.SuccessfulPermissions, result.TotalUsers, result.CreatedFolders.Count);
                }
                else
                {
                    _logger.LogWarning("Bulk Google Drive permission initialization completed with errors: {FailureCount} failures. Errors: {Errors}",
                        result.FailedPermissions, string.Join("; ", result.Errors.Take(5))); // Log first 5 errors
                }

                // Respond with the result
                await context.RespondAsync(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing bulk Google Drive permission initialization");
                
                var errorResponse = new GoogleDrivePermissionSetupResponse
                {
                    Success = false,
                    Message = $"Bulk initialization failed: {ex.Message}",
                    Errors = { ex.Message }
                };

                await context.RespondAsync(errorResponse);
                throw; // Re-throw to trigger retry mechanism
            }
        }
    }

    /// <summary>
    /// Consumer for setting up database folder permissions for new users
    /// </summary>
    public class SetupUserFolderPermissionsConsumer : IConsumer<SetupUserFolderPermissionsCommand>
    {
        private readonly IFolderPermissionService _folderPermissionService;
        private readonly ILogger<SetupUserFolderPermissionsConsumer> _logger;

        public SetupUserFolderPermissionsConsumer(
            IFolderPermissionService folderPermissionService,
            ILogger<SetupUserFolderPermissionsConsumer> logger)
        {
            _folderPermissionService = folderPermissionService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<SetupUserFolderPermissionsCommand> context)
        {
            var command = context.Message;

            try
            {
                _logger.LogInformation("Processing database folder permission setup for user {UserId} ({UserEmail}) in department {DepartmentId} with role {UserRole}",
                    command.UserId, command.UserEmail, command.DepartmentId, command.UserRole);

                var permissionsCreated = await _folderPermissionService.GrantDefaultFolderPermissionsToNewUserAsync(
                    command.UserId,
                    command.DepartmentId,
                    command.UserRole);

                _logger.LogInformation("Successfully setup {Count} database folder permissions for user {UserId}",
                    permissionsCreated, command.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to setup database folder permissions for user {UserId} ({UserEmail})",
                    command.UserId, command.UserEmail);

                // Don't throw - this is a background process and shouldn't fail user creation
                // The user can still access folders through the default permission logic
            }
        }
    }
}

using Auth.API.Services.Implement;
using Auth.API.Services.Interface;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Command;
using Shared.DTOs;

namespace Auth.API.Consumers
{
    public class GetDepartmentNamesConsumer : IConsumer<GetDepartmentNamesCommand>
    {
        private readonly IDepartmentService _departmentService;  // ✅ FIX: Use DepartmentService
        private readonly ILogger<GetDepartmentNamesConsumer> _logger;

        public GetDepartmentNamesConsumer(
            IDepartmentService departmentService,  // ✅ FIX: Inject DepartmentService
            ILogger<GetDepartmentNamesConsumer> logger)
        {
            _departmentService = departmentService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<GetDepartmentNamesCommand> context)
        {
            try
            {
                var command = context.Message;
                _logger.LogInformation("Getting department names for {Count} departments", command.DepartmentIds.Count);

                // ✅ FIX: Convert Guid list to string list for DepartmentService
                var stringIds = command.DepartmentIds.Select(id => id.ToString()).ToList();

                _logger.LogDebug("Converting {Count} Guid IDs to string IDs", stringIds.Count);

                // ✅ FIX: Call DepartmentService instead of UserSettingService
                var departmentNamesStringKeys = await _departmentService.GetDepartmentNamesByIdsAsync(stringIds);

                // ✅ FIX: Convert string keys back to Guid keys for response
                var departmentNamesGuidKeys = new Dictionary<Guid, string>();

                foreach (var kvp in departmentNamesStringKeys)
                {
                    if (Guid.TryParse(kvp.Key, out var guidKey))
                    {
                        departmentNamesGuidKeys[guidKey] = kvp.Value;
                        _logger.LogDebug("Mapped department {Id} -> {Name}", guidKey, kvp.Value);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to parse department ID: {InvalidId}", kvp.Key);
                    }
                }

                await context.RespondAsync(new GetDepartmentNamesResponse
                {
                    DepartmentNames = departmentNamesGuidKeys,  // ✅ Now has Guid keys
                    Success = true,
                    RequestId = command.RequestId
                });

                _logger.LogInformation("Successfully retrieved {Count} department names", departmentNamesGuidKeys.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting department names");
                await context.RespondAsync(new GetDepartmentNamesResponse
                {
                    DepartmentNames = new Dictionary<Guid, string>(),
                    Success = false,
                    ErrorMessage = ex.Message,
                    RequestId = context.Message.RequestId
                });
            }
        }
    }
}
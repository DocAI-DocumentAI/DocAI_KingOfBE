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
        private readonly IUserSettingService _iuserSettingService;
        private readonly ILogger<GetDepartmentNamesConsumer> _logger;

        public GetDepartmentNamesConsumer(
            IUserSettingService iuserSettingService,
            ILogger<GetDepartmentNamesConsumer> logger)
        {
            _iuserSettingService = iuserSettingService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<GetDepartmentNamesCommand> context)
        {
            try
            {
                var command = context.Message;
                _logger.LogInformation("Getting department names for {Count} departments", command.DepartmentIds.Count);

                var departmentNames = await _iuserSettingService.GetDepartmentNamesByIdsAsync(command.DepartmentIds.ToList());

                await context.RespondAsync(new GetDepartmentNamesResponse
                {
                    DepartmentNames = departmentNames,
                    Success = true,
                    RequestId = command.RequestId
                });

                _logger.LogInformation("Successfully retrieved {Count} department names", departmentNames.Count);
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

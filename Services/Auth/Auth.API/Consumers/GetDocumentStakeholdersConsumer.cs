using Auth.API.Services.Interface;
using MassTransit;
using Shared.Command;
using Shared.DTOs;

namespace Auth.API.Consumers
{
    public class GetDocumentStakeholdersConsumer : IConsumer<GetDocumentStakeholdersCommand>
    {
        private readonly IUserService _userService;
        private readonly ILogger<GetDocumentStakeholdersConsumer> _logger;

        public GetDocumentStakeholdersConsumer(
            IUserService userService,
            ILogger<GetDocumentStakeholdersConsumer> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<GetDocumentStakeholdersCommand> context)
        {
            try
            {
                var command = context.Message;
                _logger.LogInformation("Getting stakeholders for document {DocumentId}", command.DocumentId);

                // For now, return empty list - implement actual logic based on your business rules
                // This could involve checking document permissions, ownership, etc.
                var stakeholders = new List<UserDto>();

                await context.RespondAsync(new GetDocumentStakeholdersResponse
                {
                    Stakeholders = stakeholders,
                    Success = true,
                    RequestId = command.RequestId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting document stakeholders");
                await context.RespondAsync(new GetDocumentStakeholdersResponse
                {
                    Stakeholders = new List<UserDto>(),
                    Success = false,
                    ErrorMessage = ex.Message,
                    RequestId = context.Message.RequestId
                });
            }
        }
    }
}

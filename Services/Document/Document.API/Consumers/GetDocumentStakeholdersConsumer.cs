using MassTransit;
using Shared.Command;
using Shared.DTOs;

namespace Document.API.Consumers
{
    public class GetDocumentStakeholdersConsumer : IConsumer<GetDocumentStakeholdersCommand>
    {
        //private readonly IDocumentStakeholderService _stakeholderService;
        private readonly ILogger<GetDocumentStakeholdersConsumer> _logger;

        public GetDocumentStakeholdersConsumer(
            //IDocumentStakeholderService stakeholderService,
            ILogger<GetDocumentStakeholdersConsumer> logger)
        {
            //_stakeholderService = stakeholderService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<GetDocumentStakeholdersCommand> context)
        {
            try
            {
                var command = context.Message;
                _logger.LogInformation("Getting stakeholders for document {DocumentId}", command.DocumentId);

                //var stakeholders = await _stakeholderService.GetDocumentStakeholdersAsync(command.DocumentId);

                //await context.RespondAsync(new GetDocumentStakeholdersResponse
                //{
                //    Stakeholders = stakeholders,
                //    Success = true,
                //    RequestId = command.RequestId
                //});
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

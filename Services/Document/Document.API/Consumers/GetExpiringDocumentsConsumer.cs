using Document.API.Services.Interfaces;
using MassTransit;
using Shared.Command;
using Shared.DTOs;
using Shared.Models;

namespace Document.API.Consumers
{
    public class GetExpiringDocumentsConsumer : IConsumer<GetExpiringDocumentsCommand>
    {
        private readonly IDocumentExpirationService _expirationService;
        private readonly ILogger<GetExpiringDocumentsConsumer> _logger;

        public GetExpiringDocumentsConsumer(
            IDocumentExpirationService expirationService,
            ILogger<GetExpiringDocumentsConsumer> logger)
        {
            _expirationService = expirationService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<GetExpiringDocumentsCommand> context)
        {
            try
            {
                _logger.LogInformation("Processing GetExpiringDocumentsCommand for date {WarningDate}",
                    context.Message.WarningDate);

                var documents = await _expirationService.GetExpiringDocumentsAsync(context.Message.WarningDate);

                await context.RespondAsync(new GetExpiringDocumentsResponse
                {
                    Documents = documents,
                    Success = true,
                    RequestId = context.Message.RequestId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing GetExpiringDocumentsCommand");
                await context.RespondAsync(new GetExpiringDocumentsResponse
                {
                    Documents = new List<DocumentExpirationDto>(),
                    Success = false,
                    ErrorMessage = ex.Message,
                    RequestId = context.Message.RequestId
                });
            }
        }
    }
}

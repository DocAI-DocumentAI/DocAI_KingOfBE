using Document.API.Services.Interfaces;
using MassTransit;
using Shared.Command;
using Shared.DTOs;

namespace Document.API.Consumers
{
    public class DeactivateDocumentWarningsConsumer : IConsumer<DeactivateDocumentWarningsCommand>
    {
        private readonly IDocumentExpirationService _expirationService;
        private readonly ILogger<DeactivateDocumentWarningsConsumer> _logger;

        public DeactivateDocumentWarningsConsumer(
            IDocumentExpirationService expirationService,
            ILogger<DeactivateDocumentWarningsConsumer> logger)
        {
            _expirationService = expirationService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<DeactivateDocumentWarningsCommand> context)
        {
            try
            {
                _logger.LogInformation("Processing DeactivateDocumentWarningsCommand for document {DocumentId}",
                    context.Message.DocumentId);

                var result = await _expirationService.DeactivateDocumentWarningsAsync(
                    context.Message.DocumentId,
                    context.Message.Version);

                await context.RespondAsync(new DeactivateDocumentWarningsResponse
                {
                    Success = result,
                    RequestId = context.Message.RequestId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing DeactivateDocumentWarningsCommand");
                await context.RespondAsync(new DeactivateDocumentWarningsResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    RequestId = context.Message.RequestId
                });
            }
        }
    }
}
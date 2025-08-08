using Document.API.Services.Interfaces;
using MassTransit;
using Shared.Command;
using Shared.DTOs;

namespace Document.API.Consumers
{
    public class UpdateDocumentStatusConsumer : IConsumer<UpdateDocumentStatusCommand>
    {
        private readonly IDocumentExpirationService _expirationService;
        private readonly ILogger<UpdateDocumentStatusConsumer> _logger;

        public UpdateDocumentStatusConsumer(
            IDocumentExpirationService expirationService,
            ILogger<UpdateDocumentStatusConsumer> logger)
        {
            _expirationService = expirationService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<UpdateDocumentStatusCommand> context)
        {
            try
            {
                var success = await _expirationService.UpdateDocumentStatusAsync(
                    context.Message.DocumentId,
                    context.Message.Version,
                    context.Message.NewStatus);

                await context.RespondAsync(new UpdateDocumentStatusResponse
                {
                    Success = success,
                    RequestId = context.Message.RequestId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating document status");

                await context.RespondAsync(new UpdateDocumentStatusResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    RequestId = context.Message.RequestId
                });
            }
        }
    }
}

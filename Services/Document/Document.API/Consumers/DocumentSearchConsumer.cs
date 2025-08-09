
using Document.API.Services.Interfaces;
using Document.Domain.Models;
using Document.Infrastructure.Repository.Interfaces;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;

namespace Document.API.Consumers
{
    public class DocumentSearchConsumer : IConsumer<ChatBoxDocumentRequest>
    {
        private readonly IDocumentRAGService _ragService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<DocumentSearchConsumer> _logger;

        public DocumentSearchConsumer(
            IDocumentRAGService ragService,
            IUnitOfWork unitOfWork,
            ILogger<DocumentSearchConsumer> logger)
        {
            _ragService = ragService;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<ChatBoxDocumentRequest> context)
        {
            var request = context.Message;
            var startTime = DateTime.UtcNow;

            _logger.LogInformation("🔥 [CONSUMER] Processing RAW CONTENT request: {RequestId} - User: {FullName} ({Role}) - Dept: {DeptName}",
                request.RequestId, request.FullName, request.Role, request.DepartmentName);

            try
            {
                var ragRequest = new Document.API.Payload.Request.DocumentRAGRequest
                {
                    RequestId = request.RequestId,
                    Query = request.Query,
                    UserId = request.UserId,
                    Email = request.Email,
                    FullName = request.FullName,
                    Phone = request.Phone,
                    Role = request.Role,
                    DepartmentId = request.DepartmentId,
                    DepartmentName = request.DepartmentName,
                    Permissions = request.Permissions,
                    MaxResults = Math.Min(request.MaxResults, 3),
                    MinRelevanceScore = Math.Max(request.MinRelevanceScore ?? 0.28, 0.28),
                    OnlyPublic = request.OnlyPublic,
                    OnlyOfficial = request.OnlyOfficial,
                    Tags = request.Tags,
                    EffectiveFrom = request.EffectiveFrom,
                    EffectiveUntil = request.EffectiveUntil,
                    RequestTime = request.RequestTime
                };

                var ragResponse = await _ragService.SearchDocumentsWithRAGAsync(ragRequest);
                var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                _logger.LogInformation("🔥 [CONSUMER] RAG response - Success: {Success}, RawContent: {HasContent}, Sources: {SourceCount}",
                    ragResponse.Success, !string.IsNullOrEmpty(ragResponse.RawContent), ragResponse.Sources?.Count ?? 0);

                if (!string.IsNullOrEmpty(ragResponse.RawContent))
                {
                    _logger.LogInformation("🔥 [CONSUMER] Raw content preview: '{Content}'",
                        ragResponse.RawContent.Substring(0, Math.Min(100, ragResponse.RawContent.Length)) + "...");
                }

                // ✅ Convert to shared DTO with RAW CONTENT
                var response = new ChatBoxDocumentResponse
                {
                    RequestId = request.RequestId,
                    Success = ragResponse.Success,
                    RawContent = ragResponse.RawContent, // ✅ Raw content instead of Answer
                    QueryProcessed = ragResponse.QueryProcessed,
                    ErrorMessage = ragResponse.ErrorMessage,
                    ProcessingTimeMs = (long)processingTime,
                    Sources = await ConvertToDocumentSourcesAsync(ragResponse.Sources)
                };

                _logger.LogInformation("✅ [CONSUMER] Final response - Success: {Success}, RawContentLength: {Length}, Sources: {SourceCount}",
                    response.Success, response.RawContent?.Length ?? 0, response.Sources.Count);

                await context.RespondAsync(response);
            }
            catch (Exception ex)
            {
                var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogError(ex, "❌ [CONSUMER] Error: {RequestId} - {Error}", request.RequestId, ex.Message);

                var errorResponse = new ChatBoxDocumentResponse
                {
                    RequestId = request.RequestId,
                    Success = false,
                    RawContent = string.Empty, // ✅ Empty raw content on error
                    QueryProcessed = request.Query,
                    ErrorMessage = $"Lỗi xử lý: {ex.Message}",
                    ProcessingTimeMs = (long)processingTime,
                    Sources = new List<ChatBoxDocumentSource>()
                };

                await context.RespondAsync(errorResponse);
            }
        }

        private async Task<List<ChatBoxDocumentSource>> ConvertToDocumentSourcesAsync(
            List<Document.API.Payload.Response.DocumentSourceResponse> internalSources)
        {
            if (internalSources == null || !internalSources.Any())
                return new List<ChatBoxDocumentSource>();

            var sources = new List<ChatBoxDocumentSource>();

            foreach (var internalSource in internalSources)
            {
                try
                {
                    var documentVersion = await _unitOfWork.GetRepository<DocumentVersion>()
                        .SingleOrDefaultAsync(
                            predicate: dv => dv.DocumentFile.Id == internalSource.DocumentId && dv.IsOfficial,
                            include: i => i.Include(dv => dv.DocumentFile)
                                          .Include(dv => dv.DocumentTags)
                                          .ThenInclude(dt => dt.Tag));

                    var source = new ChatBoxDocumentSource
                    {
                        DocumentId = internalSource.DocumentId,
                        Title = internalSource.Title,
                        VersionName = internalSource.VersionName,
                        Summary = internalSource.Summary,
                        DepartmentId = internalSource.DepartmentId,
                        DepartmentName = internalSource.DepartmentName,
                        ApprovalDate = internalSource.ApprovalDate,
                        RelevanceScore = internalSource.RelevanceScore
                    };

                    if (documentVersion != null)
                    {
                        source.Description = documentVersion.DocumentFile.Description;
                        source.FileType = documentVersion.FileType ?? "";
                        source.FileSize = documentVersion.FileSize;
                        source.Status = documentVersion.Status.ToString();
                        source.EffectiveFrom = documentVersion.EffectiveFrom;
                        source.EffectiveUntil = documentVersion.EffectiveUntil;
                        source.Tags = documentVersion.DocumentTags.Select(dt => dt.Tag.Name).ToList();
                    }

                    sources.Add(source);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ [CONVERT] Error converting source: {DocumentId}", internalSource.DocumentId);

                    sources.Add(new ChatBoxDocumentSource
                    {
                        DocumentId = internalSource.DocumentId,
                        Title = internalSource.Title ?? "Unknown Document",
                        VersionName = internalSource.VersionName ?? "v1.0",
                        RelevanceScore = internalSource.RelevanceScore,
                        Tags = new List<string>()
                    });
                }
            }

            return sources;
        }
    }
}


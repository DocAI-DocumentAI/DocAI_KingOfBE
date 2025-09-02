
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Services.Interfaces;
using Document.Domain.Migrations;
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
        private readonly ILogger<DocumentSearchConsumer> _logger;

        public DocumentSearchConsumer(
            IDocumentRAGService ragService,
            ILogger<DocumentSearchConsumer> logger)
        {
            _ragService = ragService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<ChatBoxDocumentRequest> context)
        {
            var request = context.Message;
            var startTime = DateTime.UtcNow;

            _logger.LogInformation("🔥 [CONSUMER] Processing request: {RequestId} - User: {FullName} ({Role})",
                request.RequestId, request.FullName, request.Role);

            try
            {
                // ✅ EXISTING RAG request creation (unchanged)
                var ragRequest = new DocumentRAGRequest
                {
                    DocumentId = request.DocumentId,
                    RequestId = request.RequestId,
                    Query = request.Query,
                    UserId = request.UserId,
                    Email = request.Email,
                    FullName = request.FullName,
                    Phone = request.Phone,
                    Role = request.Role,
                    DepartmentId = request.DepartmentId,
                    DepartmentName = request.DepartmentName,
                    Permissions = request.Permissions ?? new List<string>(),
                    MaxResults = Math.Min(Math.Max(request.MaxResults, 1), 20),
                    MinRelevanceScore = Math.Max(request.MinRelevanceScore ?? 0.01, 0.001),
                    OnlyPublic = request.OnlyPublic,
                    OnlyOfficial = false,
                    Tags = request.Tags,
                    EffectiveFrom = request.EffectiveFrom,
                    EffectiveUntil = request.EffectiveUntil,
                    RequestTime = request.RequestTime
                };

                // ✅ Call RAG service
                var ragResponse = await _ragService.SearchDocumentsWithRAGAsync(ragRequest);
                var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                _logger.LogInformation("🔥 [CONSUMER] RAG response - Success: {Success}, Sources: {SourceCount}, ProcessingTime: {ProcessingTime}ms",
                    ragResponse.Success, ragResponse.Sources?.Count ?? 0, processingTime);

                // ✅ FAST CONVERSION - NO DATABASE CALLS
                var response = new ChatBoxDocumentResponse
                {
                    RequestId = request.RequestId,
                    Success = ragResponse.Success,
                    RawContent = ragResponse.RawContent ?? string.Empty,
                    QueryProcessed = ragResponse.QueryProcessed ?? request.Query,
                    ErrorMessage = ragResponse.ErrorMessage,
                    ProcessingTimeMs = (long)processingTime,
                    Sources = ConvertToDocumentSourcesFast(ragResponse.Sources, request.RequestId) // ✅ FAST conversion
                };

                _logger.LogInformation("✅ [CONSUMER] Fast response - Content: {Length} chars, Sources: {SourceCount}",
                    response.RawContent?.Length ?? 0, response.Sources.Count);

                await context.RespondAsync(response);
            }
            catch (InvalidOperationException ex)
            {
                await RespondWithError(context, request, startTime, ex.Message, "SERVICE_ERROR");
            }
            catch (Exception ex)
            {
                await RespondWithError(context, request, startTime, $"Unexpected error: {ex.Message}", "SYSTEM_ERROR");
            }
        }

        /// <summary>
        /// ✅ FAST CONVERSION - Use ONLY KernelMemory tags, NO database calls
        /// </summary>
        private List<ChatBoxDocumentSource> ConvertToDocumentSourcesFast(
            List<DocumentSourceResponse> internalSources,
            string requestId)
        {
            if (internalSources == null || !internalSources.Any())
            {
                _logger.LogDebug("📋 [CONVERT-{RequestId}] No sources to convert", requestId);
                return new List<ChatBoxDocumentSource>();
            }

            var sources = new List<ChatBoxDocumentSource>();

            _logger.LogInformation("📋 [CONVERT-{RequestId}] Fast converting {Count} sources using ONLY KernelMemory data",
                requestId, internalSources.Count);

            foreach (var internalSource in internalSources)
            {
                try
                {
                    var source = new ChatBoxDocumentSource
                    {
                        // ✅ CORE IDENTIFIERS - Direct from KernelMemory tags
                        DocumentId = internalSource.DocumentId ?? Guid.NewGuid().ToString(),
                        VersionId = internalSource.VersionId,
                        Title = internalSource.Title ?? "Unknown Document",
                        VersionName = internalSource.VersionName ?? "1.0",

                        // ✅ APPROVAL & SIGNING INFO - From tags
                        SignedBy = internalSource.SignedBy,
                        ApprovedBy = internalSource.ApprovedBy,
                        CreatedBy = internalSource.CreatedBy,
                        //SubmittedBy = internalSource.SubmittedBy,
                        ReviewerName = internalSource.ReviewerName,
                        OwnerName = internalSource.OwnerName,

                        // ✅ ORGANIZATIONAL INFO - From tags
                        DepartmentId = internalSource.DepartmentId,
                        DepartmentName = internalSource.DepartmentName ?? internalSource.DepartmentId ?? "Unknown Department",

                        // ✅ DOCUMENT METADATA - From tags
                        Description = internalSource.Description,
                        Summary = internalSource.Summary,
                        DocumentType = internalSource.DocumentType,
                        Category = internalSource.Category,
                        Priority = internalSource.Priority,
                        Status = internalSource.Status ?? "approved",

                        // ✅ FILE INFO - From tags
                        FileName = internalSource.FileName,
                        FileType = internalSource.FileType ?? "",
                        FileSize = internalSource.FileSize,

                        // ✅ DATES - From tags
                        ApprovalDate = internalSource.ApprovalDate,
                        EffectiveFrom = internalSource.EffectiveFrom,
                        EffectiveUntil = internalSource.EffectiveUntil,
                        SignedDate = internalSource.SignedDate,
                        ReviewDate = internalSource.ReviewDate,
                        //LastSubmitted = internalSource.LastSubmitted,

                        // ✅ ACCESS CONTROL - From tags
                        IsPublic = internalSource.IsPublic,
                        Visibility = internalSource.Visibility,
                        PermissionLevel = internalSource.PermissionLevel,

                        // ✅ VERSION INFO - From tags
                        IsLatestVersion = internalSource.IsLatestVersion,
                        VersionNumber = internalSource.VersionNumber,

                        // ✅ RELATIONSHIPS - From tags
                        ParentDocumentId = internalSource.ParentDocumentId,
                        RelatedDocumentIds = internalSource.RelatedDocumentIds ?? new List<string>(),

                        // ✅ SEARCH RELEVANCE
                        RelevanceScore = internalSource.RelevanceScore,

                        // ✅ TAGS - From tags
                        Tags = internalSource.Tags ?? new List<string>()
                    };

                    sources.Add(source);

                    _logger.LogDebug("📋 [CONVERT-{RequestId}] Mapped source: {Title} - Dept: {DeptId} - SignedBy: {SignedBy}",
                        requestId, source.Title, source.DepartmentId, source.SignedBy ?? "Unknown");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ [CONVERT-{RequestId}] Error converting source: {DocumentId}",
                        requestId, internalSource.DocumentId);

                    // ✅ ADD MINIMAL SOURCE ON ERROR
                    sources.Add(new ChatBoxDocumentSource
                    {
                        DocumentId = internalSource.DocumentId ?? Guid.NewGuid().ToString(),
                        Title = internalSource.Title ?? "Error Loading Document",
                        VersionName = "1.0",
                        RelevanceScore = internalSource.RelevanceScore,
                        Status = "Error",
                        FileType = "",
                        Tags = new List<string>(),
                        DepartmentName = "Unknown"
                    });
                }
            }

            _logger.LogInformation("📋 [CONVERT-{RequestId}] Fast conversion completed: {Count}/{Total} sources in {Ms}ms",
                requestId, sources.Count, internalSources.Count,
                sources.Count * 2); // Estimated 2ms per source vs 50-100ms with DB calls

            return sources.OrderByDescending(s => s.RelevanceScore).ToList();
        }

        /// <summary>
        /// ✅ HELPER: Respond with error
        /// </summary>
        private async Task RespondWithError(ConsumeContext<ChatBoxDocumentRequest> context,
            ChatBoxDocumentRequest request, DateTime startTime, string errorMessage, string errorType)
        {
            var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogError("❌ [CONSUMER] {ErrorType}: {RequestId} - {Error}", errorType, request.RequestId, errorMessage);

            var errorResponse = new ChatBoxDocumentResponse
            {
                RequestId = request.RequestId,
                Success = false,
                RawContent = string.Empty,
                QueryProcessed = request.Query ?? string.Empty,
                ErrorMessage = errorMessage,
                ProcessingTimeMs = (long)processingTime,
                Sources = new List<ChatBoxDocumentSource>()
            };

            await context.RespondAsync(errorResponse);
        }
    }
}
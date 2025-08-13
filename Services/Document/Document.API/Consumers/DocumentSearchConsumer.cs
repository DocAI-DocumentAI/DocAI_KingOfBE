
using Document.API.Payload.Request;
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

            _logger.LogInformation("🔥 [CONSUMER] Processing request: {RequestId} - User: {FullName} ({Role}) - Dept: {DeptName} - Query: '{Query}'",
                request.RequestId, request.FullName, request.Role, request.DepartmentName,
                request.Query?.Substring(0, Math.Min(50, request.Query?.Length ?? 0)));

            try
            {
                // ✅ CREATE COMPREHENSIVE RAG REQUEST
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
                    MaxResults = Math.Min(Math.Max(request.MaxResults, 1), 10),
                    MinRelevanceScore = Math.Max(request.MinRelevanceScore ?? 0.01, 0.001),
                    OnlyPublic = request.OnlyPublic,
                    OnlyOfficial = false, // ✅ Not filtering by official status
                    Tags = request.Tags,
                    EffectiveFrom = request.EffectiveFrom,
                    EffectiveUntil = request.EffectiveUntil,
                    RequestTime = request.RequestTime
                };

                _logger.LogDebug("🔥 [CONSUMER] RAG Request Details - MaxResults: {MaxResults}, MinRelevance: {MinRelevance}, OnlyPublic: {OnlyPublic}, Role: {Role}",
                    ragRequest.MaxResults, ragRequest.MinRelevanceScore, ragRequest.OnlyPublic, ragRequest.Role);

                // ✅ CALL RAG SERVICE
                var ragResponse = await _ragService.SearchDocumentsWithRAGAsync(ragRequest);
                var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                _logger.LogInformation("🔥 [CONSUMER] RAG response - Success: {Success}, Content: {HasContent} ({Length} chars), Sources: {SourceCount}, ProcessingTime: {ProcessingTime}ms",
                    ragResponse.Success,
                    !string.IsNullOrEmpty(ragResponse.RawContent),
                    ragResponse.RawContent?.Length ?? 0,
                    ragResponse.Sources?.Count ?? 0,
                    processingTime);

                // ✅ CONVERT TO CHATBOX DTO
                var response = new ChatBoxDocumentResponse
                {
                    RequestId = request.RequestId,
                    Success = ragResponse.Success,
                    RawContent = ragResponse.RawContent ?? string.Empty,
                    QueryProcessed = ragResponse.QueryProcessed ?? request.Query,
                    ErrorMessage = ragResponse.ErrorMessage,
                    ProcessingTimeMs = (long)processingTime,
                    Sources = await ConvertToDocumentSources(ragResponse.Sources, request.RequestId)
                };

                // ✅ VALIDATION
                if (response.Success && string.IsNullOrEmpty(response.RawContent) && !response.Sources.Any())
                {
                    _logger.LogInformation("⚠️ [CONSUMER] Successful response but no content or sources found for query: '{Query}'", request.Query);
                }

                _logger.LogInformation("✅ [CONSUMER] Final response - Success: {Success}, RawContentLength: {Length}, Sources: {SourceCount}",
                    response.Success, response.RawContent?.Length ?? 0, response.Sources.Count);

                await context.RespondAsync(response);
            }
            catch (Exception ex)
            {
                var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogError(ex, "❌ [CONSUMER] Error processing request: {RequestId} - {Error}", request.RequestId, ex.Message);

                var errorResponse = new ChatBoxDocumentResponse
                {
                    RequestId = request.RequestId,
                    Success = false,
                    RawContent = string.Empty,
                    QueryProcessed = request.Query ?? string.Empty,
                    ErrorMessage = $"Lỗi xử lý tài liệu: {ex.Message}",
                    ProcessingTimeMs = (long)processingTime,
                    Sources = new List<ChatBoxDocumentSource>()
                };

                await context.RespondAsync(errorResponse);
            }
        }

        /// <summary>
        /// ✅ CONVERT TO CHATBOX DOCUMENT SOURCES
        /// </summary>
        private async Task<List<ChatBoxDocumentSource>> ConvertToDocumentSources(
            List<Document.API.Payload.Response.DocumentSourceResponse> internalSources,
            string requestId)
        {
            if (internalSources == null || !internalSources.Any())
            {
                _logger.LogDebug("📋 [CONVERT-{RequestId}] No internal sources to convert", requestId);
                return new List<ChatBoxDocumentSource>();
            }

            var sources = new List<ChatBoxDocumentSource>();

            _logger.LogDebug("📋 [CONVERT-{RequestId}] Converting {Count} internal sources", requestId, internalSources.Count);

            foreach (var internalSource in internalSources)
            {
                try
                {
                    var source = new ChatBoxDocumentSource
                    {
                        DocumentId = internalSource.DocumentId,
                        Title = internalSource.Title ?? "Unknown Document",
                        VersionName = internalSource.VersionName ?? "1",
                        Summary = internalSource.Summary,
                        DepartmentId = internalSource.DepartmentId,
                        ApprovalDate = internalSource.ApprovalDate,
                        RelevanceScore = internalSource.RelevanceScore,
                        EffectiveFrom = internalSource.EffectiveFrom,
                        EffectiveUntil = internalSource.EffectiveUntil,
                        FileType = internalSource.FileType ?? "",
                    };

                    // ✅ ENHANCE WITH DATABASE INFO IF AVAILABLE
                    try
                    {
                        DocumentVersion documentVersion = null;

                        // Try with VersionId first (more specific)
                        if (!string.IsNullOrEmpty(internalSource.VersionName))
                        {
                            documentVersion = await _unitOfWork.GetRepository<DocumentVersion>()
                                .SingleOrDefaultAsync(
                                    predicate: dv => dv.Id == internalSource.VersionName,
                                    include: i => i.Include(dv => dv.DocumentFile)
                                                  .Include(dv => dv.DocumentTags)
                                                  .ThenInclude(dt => dt.Tag));
                        }

                        // Fallback to DocumentId if VersionId didn't work
                        if (documentVersion == null && !string.IsNullOrEmpty(internalSource.DocumentId))
                        {
                            documentVersion = await _unitOfWork.GetRepository<DocumentVersion>()
                                .SingleOrDefaultAsync(
                                    predicate: dv => dv.DocumentFile.Id == internalSource.DocumentId,
                                    include: i => i.Include(dv => dv.DocumentFile)
                                                  .Include(dv => dv.DocumentTags)
                                                  .ThenInclude(dt => dt.Tag));
                        }

                        if (documentVersion != null)
                        {
                            // ✅ ENHANCE WITH COMPLETE DATABASE DETAILS
                            source.Title = documentVersion.Title ?? source.Title;
                            source.FileType = documentVersion.FileType ?? source.FileType;
                            source.Status = documentVersion.Status.ToString();
                            source.EffectiveFrom = documentVersion.EffectiveFrom ?? source.EffectiveFrom;
                            source.EffectiveUntil = documentVersion.EffectiveUntil ?? source.EffectiveUntil;
                            source.Summary = documentVersion.Summary ?? source.Summary;
                            source.VersionName = documentVersion.VersionName ?? source.VersionName;
                            source.DepartmentId = documentVersion.DocumentFile.DepartmentId ?? source.DepartmentId;
                            source.Tags = documentVersion.DocumentTags?
                                .Select(dt => dt.Tag?.Name)
                                .Where(t => !string.IsNullOrEmpty(t))
                                .ToList() ?? source.Tags;

                            _logger.LogDebug("📋 [CONVERT-{RequestId}] Enhanced source {DocId}/{VersionId} with database info",
                                requestId, internalSource.DocumentId, internalSource.VersionName);
                        }
                        else
                        {
                            _logger.LogDebug("📋 [CONVERT-{RequestId}] Document {DocId}/{VersionId} not found in database, using basic info",
                                requestId, internalSource.DocumentId, internalSource.VersionName);
                        }
                    }
                    catch (Exception dbEx)
                    {
                        _logger.LogWarning(dbEx, "📋 [CONVERT-{RequestId}] Database lookup failed for {DocumentId}/{VersionId}, using basic info",
                            requestId, internalSource.DocumentId, internalSource.VersionName);
                    }

                    sources.Add(source);

                    _logger.LogDebug("📋 [CONVERT-{RequestId}] Successfully converted source: {Title} (Relevance: {Relevance:F3})",
                        requestId, source.Title, source.RelevanceScore);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ [CONVERT-{RequestId}] Error converting source: {DocumentId}/{VersionId}",
                        requestId, internalSource.DocumentId, internalSource.VersionName);

                    // ✅ ADD MINIMAL SOURCE ON ERROR
                    sources.Add(new ChatBoxDocumentSource
                    {
                        DocumentId = internalSource.DocumentId ?? Guid.NewGuid().ToString(),
                        Title = internalSource.Title ?? "Error Loading Document",
                        VersionName = internalSource.VersionName ?? "1.0",
                        RelevanceScore = internalSource.RelevanceScore,
                        Tags = new List<string>(),
                        Status = "Error",
                        FileType = "",
                    });
                }
            }

            _logger.LogInformation("📋 [CONVERT-{RequestId}] Successfully converted {Count}/{Total} sources",
                requestId, sources.Count, internalSources.Count);

            // ✅ SORT BY RELEVANCE
            return sources.OrderByDescending(s => s.RelevanceScore).ToList();
        }
    }
}
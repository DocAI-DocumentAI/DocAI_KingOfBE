
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

            _logger.LogInformation("🔥🔥🔥 [CONSUMER] Processing document search: {RequestId} - Query: {Query} - User: {UserId}",
                request.RequestId, request.Query, request.UserId);

            Console.WriteLine($"🔥🔥🔥 [CONSUMER] RECEIVED REQUEST: {request.RequestId}");
            Console.WriteLine($"🔥 [CONSUMER] Query: '{request.Query}'");
            Console.WriteLine($"🔥 [CONSUMER] UserId: {request.UserId}");
            Console.WriteLine($"🔥 [CONSUMER] MaxResults: {request.MaxResults}");
            Console.WriteLine($"🔥 [CONSUMER] MinRelevanceScore: {request.MinRelevanceScore}");

            try
            {
                Console.WriteLine($"🔥 [CONSUMER] Converting to internal RAG request...");

                // ✅ Convert shared DTO to internal RAG request
                var ragRequest = new Document.API.Payload.Request.DocumentRAGRequest
                {
                    RequestId = request.RequestId,
                    Query = request.Query,
                    MaxResults = request.MaxResults,
                    MinRelevanceScore = request.MinRelevanceScore,
                    OnlyPublic = request.OnlyPublic,
                    OnlyOfficial = request.OnlyOfficial,
                    Tags = request.Tags,
                    EffectiveFrom = request.EffectiveFrom,
                    EffectiveUntil = request.EffectiveUntil,
                    RequestTime = request.RequestTime
                };

                Console.WriteLine($"🔥 [CONSUMER] Calling RAG service...");

                // ✅ Process RAG search
                var ragResponse = await _ragService.SearchDocumentsWithRAGAsync(ragRequest);
                var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                Console.WriteLine($"🔥 [CONSUMER] RAG service response - Success: {ragResponse.Success}");
                Console.WriteLine($"🔥 [CONSUMER] RAG answer length: {ragResponse.Answer?.Length ?? 0}");
                Console.WriteLine($"🔥 [CONSUMER] RAG sources count: {ragResponse.Sources?.Count ?? 0}");
                Console.WriteLine($"🔥 [CONSUMER] RAG error: {ragResponse.ErrorMessage ?? "None"}");

                // ✅ Convert internal response to shared DTO
                var response = new ChatBoxDocumentResponse
                {
                    RequestId = request.RequestId,
                    Success = ragResponse.Success,
                    Answer = ragResponse.Answer,
                    QueryProcessed = ragResponse.QueryProcessed,
                    ErrorMessage = ragResponse.ErrorMessage,
                    ProcessingTimeMs = (long)processingTime,
                    Sources = await ConvertToDocumentSourcesAsync(ragResponse.Sources)
                };

                Console.WriteLine($"🔥 [CONSUMER] Final response - Success: {response.Success}");
                Console.WriteLine($"🔥 [CONSUMER] Final answer: '{response.Answer?.Substring(0, Math.Min(100, response.Answer?.Length ?? 0))}...'");
                Console.WriteLine($"🔥 [CONSUMER] Final sources: {response.Sources?.Count ?? 0}");

                _logger.LogInformation("✅ [CONSUMER] Document search completed: {RequestId} - Success: {Success} - Sources: {SourceCount} - Time: {ProcessingTime}ms",
                    request.RequestId, response.Success, response.Sources.Count, processingTime);

                Console.WriteLine($"🔥 [CONSUMER] Sending response back to ChatBox...");

                // ✅ Send response back to ChatBox
                await context.RespondAsync(response);

                Console.WriteLine($"✅ [CONSUMER] Response sent successfully!");
                _logger.LogInformation("✅ [CONSUMER] Response sent for request: {RequestId}", request.RequestId);
            }
            catch (Exception ex)
            {
                var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                _logger.LogError(ex, "❌ [CONSUMER] Error processing document search: {RequestId} - Time: {ProcessingTime}ms - Error: {Error}",
                    request.RequestId, processingTime, ex.Message);

                Console.WriteLine($"❌ [CONSUMER] EXCEPTION: {ex.Message}");
                Console.WriteLine($"❌ [CONSUMER] Stack trace: {ex.StackTrace}");

                // ✅ Send error response
                var errorResponse = new ChatBoxDocumentResponse
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Answer = string.Empty,
                    QueryProcessed = request.Query,
                    ErrorMessage = $"Lỗi xử lý: {ex.Message}",
                    ProcessingTimeMs = (long)processingTime,
                    Sources = new List<ChatBoxDocumentSource>()
                };

                try
                {
                    await context.RespondAsync(errorResponse);
                    Console.WriteLine($"❌ [CONSUMER] Error response sent");
                }
                catch (Exception responseEx)
                {
                    Console.WriteLine($"❌ [CONSUMER] Failed to send error response: {responseEx.Message}");
                    _logger.LogError(responseEx, "Failed to send error response for request: {RequestId}", request.RequestId);
                }
            }
        }


        // ✅ Convert internal DocumentSourceResponse to shared DocumentSource
        private async Task<List<ChatBoxDocumentSource>> ConvertToDocumentSourcesAsync(
             List<Document.API.Payload.Response.DocumentSourceResponse> internalSources)
        {
            Console.WriteLine($"🔥 [CONSUMER] Converting {internalSources?.Count ?? 0} internal sources to shared DTOs");

            if (internalSources == null || !internalSources.Any())
            {
                Console.WriteLine($"🔥 [CONSUMER] No internal sources to convert");
                return new List<ChatBoxDocumentSource>();
            }

            var sources = new List<ChatBoxDocumentSource>();

            foreach (var internalSource in internalSources)
            {
                Console.WriteLine($"🔥 [CONSUMER] Converting source: {internalSource.DocumentId} - {internalSource.Title}");

                try
                {
                    // ✅ Get additional document details from database
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

                    // ✅ Add additional details from database if available
                    if (documentVersion != null)
                    {
                        Console.WriteLine($"🔥 [CONSUMER] Found document version for {internalSource.DocumentId}");

                        source.Description = documentVersion.DocumentFile.Description;
                        source.FileType = documentVersion.FileType ?? "";
                        source.FileSize = documentVersion.FileSize;
                        source.Status = documentVersion.Status.ToString();
                        source.EffectiveFrom = documentVersion.EffectiveFrom;
                        source.EffectiveUntil = documentVersion.EffectiveUntil;

                        // ✅ Add tags
                        source.Tags = documentVersion.DocumentTags
                            .Select(dt => dt.Tag.Name)
                            .ToList();
                    }
                    else
                    {
                        Console.WriteLine($"❌ [CONSUMER] No document version found for {internalSource.DocumentId}");
                    }

                    sources.Add(source);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ [CONSUMER] Error converting source {internalSource.DocumentId}: {ex.Message}");
                    _logger.LogError(ex, "Error converting document source: {DocumentId}", internalSource.DocumentId);

                    // Add basic source info even if database lookup fails
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

            Console.WriteLine($"✅ [CONSUMER] Converted {sources.Count} sources successfully");
            return sources;
        }
    }
}


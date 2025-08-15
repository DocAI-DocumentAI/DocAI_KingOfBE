
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
            /// ✅ CONVERT TO CHATBOX DOCUMENT SOURCES - FIXED DATABASE ACCESS
            /// </summary>
            private async Task<List<ChatBoxDocumentSource>> ConvertToDocumentSources(
                List<DocumentSourceResponse> internalSources,
                string requestId)
            {
                if (internalSources == null || !internalSources.Any())
                {
                    _logger.LogDebug("📋 [CONVERT-{RequestId}] No internal sources to convert", requestId);
                    return new List<ChatBoxDocumentSource>();
                }

                var sources = new List<ChatBoxDocumentSource>();

                _logger.LogDebug("📋 [CONVERT-{RequestId}] Converting {Count} internal sources with ALL metadata",
                    requestId, internalSources.Count);

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
                            Tags = internalSource.Tags ?? new List<string>(),
                            Status = internalSource.Status,
                            IsPublic = internalSource.IsPublic,
                            // ✅ NEW FIELD MAPPING - CHỈ THÊM, không ảnh hưởng existing logic
                            VersionId = internalSource.VersionId,
                            SignedBy = internalSource.SignedBy,
                            OwnerName = internalSource.OwnerName,
                            CreatedBy = internalSource.CreatedBy,
                            ReviewerName = internalSource.ReviewerName,
                            ApprovedBy = internalSource.ApprovedBy,
                            DepartmentName = internalSource.DepartmentName,
                            SignedDate = internalSource.SignedDate,
                            ReviewDate = internalSource.ReviewDate,
                            FileSize = internalSource.FileSize,
                            FileName = internalSource.FileName,
                            DocumentType = internalSource.DocumentType,
                            Category = internalSource.Category,
                            Priority = internalSource.Priority,
                            IsLatestVersion = internalSource.IsLatestVersion,
                            VersionNumber = internalSource.VersionNumber,
                            Visibility = internalSource.Visibility,
                            PermissionLevel = internalSource.PermissionLevel,
                            ParentDocumentId = internalSource.ParentDocumentId,
                            RelatedDocumentIds = internalSource.RelatedDocumentIds ?? new List<string>(),
                            Description = internalSource.Description,
                        };

                        // ✅ OPTIONAL DATABASE ENHANCEMENT - Safe access only
                        await EnhanceSourceFromDatabase(source, internalSource, requestId);

                        sources.Add(source);

                        _logger.LogDebug("📋 [CONVERT-{RequestId}] Mapped source: {Title} with complete metadata - SignedBy: {SignedBy}, FileSize: {FileSize}KB",
                            requestId, source.Title, source.SignedBy ?? "Unknown", (source.FileSize ?? 0) / 1024.0);
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
                            VersionName = internalSource.VersionName ?? "1.0",
                            RelevanceScore = internalSource.RelevanceScore,
                            Status = "Error",
                            FileType = "",
                            Tags = new List<string>()
                        });
                    }
                }

                _logger.LogInformation("📋 [CONVERT-{RequestId}] Successfully converted {Count}/{Total} sources with COMPLETE metadata",
                    requestId, sources.Count, internalSources.Count);

                return sources.OrderByDescending(s => s.RelevanceScore).ToList();
            }

            /// <summary>
            /// ✅ SAFE DATABASE ENHANCEMENT - Only if needed and available
            /// </summary>
            private async Task EnhanceSourceFromDatabase(ChatBoxDocumentSource source,
                DocumentSourceResponse internalSource, string requestId)
            {
                try
                {
                    // ✅ Only enhance if we're missing critical info
                    if (!string.IsNullOrEmpty(source.Title) && !string.IsNullOrEmpty(source.DepartmentName))
                    {
                        _logger.LogDebug("📋 [DB-ENHANCE-{RequestId}] Source already has complete info, skipping DB lookup", requestId);
                        return;
                    }

                    DocumentVersion documentVersion = null;

                    // ✅ Try lookup by VersionId first
                    if (!string.IsNullOrEmpty(internalSource.VersionId))
                    {
                        documentVersion = await _unitOfWork.GetRepository<DocumentVersion>()
                            .SingleOrDefaultAsync(
                                predicate: dv => dv.Id == internalSource.VersionId,
                                include: i => i.Include(dv => dv.DocumentFile)
                                              .Include(dv => dv.DocumentTags)
                                              .ThenInclude(dt => dt.Tag));
                    }

                    // ✅ Fallback to DocumentId if VersionId lookup failed
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
                        // ✅ SAFE ENHANCEMENT - Only override if current value is empty
                        source.Title = !string.IsNullOrEmpty(source.Title) ? source.Title : (documentVersion.Title ?? source.Title);
                        source.FileType = !string.IsNullOrEmpty(source.FileType) ? source.FileType : (documentVersion.FileType ?? source.FileType);
                        source.Status = !string.IsNullOrEmpty(source.Status) ? source.Status : documentVersion.Status.ToString();
                        source.VersionName = !string.IsNullOrEmpty(source.VersionName) ? source.VersionName : (documentVersion.VersionName ?? source.VersionName);
                        source.Summary = !string.IsNullOrEmpty(source.Summary) ? source.Summary : (documentVersion.Summary ?? source.Summary);

                        // ✅ SAFE ACCESS to DepartmentId (always available)
                        if (string.IsNullOrEmpty(source.DepartmentId) && !string.IsNullOrEmpty(documentVersion.DocumentFile.DepartmentId))
                        {
                            source.DepartmentId = documentVersion.DocumentFile.DepartmentId;
                        }

                        // ✅ SAFE ACCESS for Department Name - Use DepartmentId to lookup if needed
                        if (string.IsNullOrEmpty(source.DepartmentName) && !string.IsNullOrEmpty(documentVersion.DocumentFile.DepartmentId))
                        {
                            //source.DepartmentName = await GetDepartmentNameSafely(documentVersion.DocumentFile.DepartmentId, requestId);
                           source.DepartmentName = "department";
                        }

                        // ✅ SAFE TAG ENHANCEMENT
                        if (!source.Tags.Any() && documentVersion.DocumentTags?.Any() == true)
                        {
                            source.Tags = documentVersion.DocumentTags
                                .Select(dt => dt.Tag?.Name)
                                .Where(t => !string.IsNullOrEmpty(t))
                                .ToList();
                        }

                        _logger.LogDebug("📋 [DB-ENHANCE-{RequestId}] Enhanced source {DocId} with database info",
                            requestId, internalSource.DocumentId);
                    }
                    else
                    {
                        _logger.LogDebug("📋 [DB-ENHANCE-{RequestId}] Document {DocId} not found in database, using DocumentRAGService metadata only",
                            requestId, internalSource.DocumentId);
                    }
                }
                catch (Exception dbEx)
                {
                    _logger.LogWarning(dbEx, "📋 [DB-ENHANCE-{RequestId}] Database enhancement failed for {DocumentId}, using DocumentRAGService metadata",
                        requestId, internalSource.DocumentId);
                }
            }

            /// <summary>
            /// ✅ SAFE DEPARTMENT NAME LOOKUP - Using DepartmentId
            /// </summary>
            //private async Task<string> GetDepartmentNameSafely(string departmentId, string requestId)
            //{
            //    try
            //    {
            //        if (string.IsNullOrEmpty(departmentId))
            //            return null;

            //        // ✅ Lookup Department by ID instead of navigation property
            //        //var department = await _unitOfWork.GetRepository<Department>()
            //        //    .SingleOrDefaultAsync(predicate: d => d.Id == departmentId);
            //        var department = "department"
            //        if (department != null)
            //        {
            //            _logger.LogDebug("📋 [DEPT-LOOKUP-{RequestId}] Found department: {DeptId} -> {DeptName}",
            //                requestId, departmentId, department.Name);
            //            return department.Name;
            //        }

            //        _logger.LogDebug("📋 [DEPT-LOOKUP-{RequestId}] Department {DeptId} not found",
            //            requestId, departmentId);
            //        return null;
            //    }
            //    catch (Exception ex)
            //    {
            //        _logger.LogWarning(ex, "📋 [DEPT-LOOKUP-{RequestId}] Failed to lookup department {DeptId}",
            //            requestId, departmentId);
            //        return null;
            //    }
            //}
        
    }
}
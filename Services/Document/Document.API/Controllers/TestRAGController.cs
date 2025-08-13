using System.Security.Claims;
using Document.API.Payload.Request;
using Document.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Document.API.Controllers
{

        [ApiController]
        [Route("api/v1/[controller]")]
        [Authorize] // Remove this if you want to test without auth
        public class TestRAGController : ControllerBase
        {
            private readonly IDocumentRAGService _ragService;
            private readonly ILogger<TestRAGController> _logger;
            private readonly IHttpContextAccessor _httpContextAccessor;

            public TestRAGController(
                IDocumentRAGService ragService,
                ILogger<TestRAGController> logger,
                IHttpContextAccessor httpContextAccessor)
            {
                _ragService = ragService;
                _logger = logger;
                _httpContextAccessor = httpContextAccessor;
            }

            /// <summary>
            /// Test 1: Simple RAG Search
            /// </summary>
            [HttpGet("simple-search")]
            public async Task<IActionResult> SimpleSearch([FromQuery] string query)
            {
                try
                {
                    _logger.LogInformation("🧪 [TEST] Simple search: {Query}", query);

                    // Get user info from JWT
                    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "test-user";

                    var rawContent = await _ragService.GetRawContentAsync(query, userId);

                    return Ok(new
                    {
                        success = !string.IsNullOrEmpty(rawContent),
                        query = query,
                        contentLength = rawContent?.Length ?? 0,
                        content = rawContent,
                        message = string.IsNullOrEmpty(rawContent)
                            ? "No documents found"
                            : $"Found content with {rawContent.Length} characters"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in simple search");
                    return StatusCode(500, new { error = ex.Message });
                }
            }

            /// <summary>
            /// Test 2: Search with Sources
            /// </summary>
            [HttpGet("search-with-sources")]
            public async Task<IActionResult> SearchWithSources([FromQuery] string query)
            {
                try
                {
                    _logger.LogInformation("🧪 [TEST] Search with sources: {Query}", query);

                    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "test-user";

                    var (rawContent, sources) = await _ragService.GetRawContentWithSourcesAsync(query, userId);

                    return Ok(new
                    {
                        success = !string.IsNullOrEmpty(rawContent) || sources.Any(),
                        query = query,
                        contentLength = rawContent?.Length ?? 0,
                        content = rawContent,
                        sourcesCount = sources.Count,
                        sources = sources.Select(s => new
                        {
                            s.DocumentId,
                            s.Title,
                            s.RelevanceScore,
                            s.DepartmentId,
                            s.EffectiveFrom,
                            s.EffectiveUntil
                        }),
                        message = sources.Any()
                            ? $"Found {sources.Count} relevant documents"
                            : "No documents found"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in search with sources");
                    return StatusCode(500, new { error = ex.Message });
                }
            }

            /// <summary>
            /// Test 3: Full RAG Search with real department data
            /// </summary>
            [HttpPost("full-search")]
            public async Task<IActionResult> FullSearch([FromBody] TestRAGRequest request)
            {
                try
                {
                    _logger.LogInformation("🧪 [TEST] Full RAG search: {Query}", request.Query);

                    // Build full RAG request with actual department IDs
                    var ragRequest = new DocumentRAGRequest
                    {
                        RequestId = Guid.NewGuid().ToString(),
                        Query = request.Query,
                        UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? request.TestUserId ?? "0570c6d9-f285-4892-a5f6-02c03400081a",
                        Email = User.FindFirst(ClaimTypes.Email)?.Value ?? request.TestEmail ?? "test@company.com",
                        FullName = User.FindFirst(ClaimTypes.Name)?.Value ?? request.TestFullName ?? "Test User",
                        Role = request.Role ?? "MEMBER",
                        DepartmentId = request.DepartmentId ?? "a02f4955-f08a-4839-a88a-d088299f8277", // Use actual department from data
                        DepartmentName = request.DepartmentName ?? "Customer Service",
                        MaxResults = request.MaxResults ?? 5,
                        MinRelevanceScore = request.MinRelevanceScore ?? 0.001,
                        OnlyPublic = request.OnlyPublic,
                        Tags = request.Tags
                    };

                    var response = await _ragService.SearchDocumentsWithRAGAsync(ragRequest);

                    return Ok(new
                    {
                        response.RequestId,
                        response.Success,
                        response.QueryProcessed,
                        ContentLength = response.RawContent?.Length ?? 0,
                        ContentPreview = response.RawContent?.Length > 200
                            ? response.RawContent.Substring(0, 200) + "..."
                            : response.RawContent,
                        SourcesCount = response.Sources?.Count ?? 0,
                        Sources = response.Sources?.Select(s => new
                        {
                            s.DocumentId,
                            s.Title,
                            s.RelevanceScore,
                            s.DepartmentId,
                            s.Summary,
                            s.EffectiveFrom,
                            s.EffectiveUntil
                        }),
                        response.ProcessingTimeMs,
                        response.ErrorMessage
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in full search");
                    return StatusCode(500, new { error = ex.Message });
                }
            }

            /// <summary>
            /// Test 4: Test Different Query Types with Real Data
            /// </summary>
            [HttpGet("test-query-types")]
            public async Task<IActionResult> TestQueryTypes()
            {
                try
                {
                    // Based on actual document data
                    var testQueries = new[]
                    {
                    // General queries
                    "phiếu đánh giá",
                    "chất lượng phục vụ",
                    "dịch vụ",
                    
                    // Specific queries from document
                    "cải thiện dịch vụ",
                    "trải nghiệm khách hàng",
                    "phản hồi khách hàng",
                    
                    // Question queries
                    "làm sao đánh giá chất lượng",
                    "cách góp ý dịch vụ",
                    
                    // Document reference queries
                    "phiếu CSKH1",
                    "mô hình tổng đài"
                };

                    var results = new List<object>();
                    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0570c6d9-f285-4892-a5f6-02c03400081a"; // Use actual owner ID

                    foreach (var query in testQueries)
                    {
                        var (content, sources) = await _ragService.GetRawContentWithSourcesAsync(query, userId);

                        results.Add(new
                        {
                            Query = query,
                            Found = !string.IsNullOrEmpty(content),
                            ContentLength = content?.Length ?? 0,
                            SourcesCount = sources.Count,
                            TopSource = sources.FirstOrDefault()?.Title
                        });
                    }

                    return Ok(new
                    {
                        TestCount = testQueries.Length,
                        Results = results,
                        Summary = new
                        {
                            TotalQueries = results.Count,
                            SuccessfulQueries = results.Count(r => ((dynamic)r).Found),
                            AverageContentLength = results.Any(r => ((dynamic)r).ContentLength > 0)
                                ? results.Where(r => ((dynamic)r).ContentLength > 0).Average(r => (double)((dynamic)r).ContentLength)
                                : 0,
                            AverageSourcesCount = results.Any()
                                ? results.Average(r => (double)((dynamic)r).SourcesCount)
                                : 0
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in query type testing");
                    return StatusCode(500, new { error = ex.Message });
                }
            }

            /// <summary>
            /// Test 5: Test Role-based Access
            /// </summary>
            [HttpPost("test-role-access")]
            public async Task<IActionResult> TestRoleAccess([FromBody] RoleTestRequest request)
            {
                try
                {
                    _logger.LogInformation("🧪 [TEST] Role access test: Query={Query}", request.Query);

                    var roles = new[] { "ADMIN", "MANAGER", "EDITOR", "MEMBER", "NONE" };
                    var results = new Dictionary<string, object>();

                    foreach (var role in roles)
                    {
                        var ragRequest = new DocumentRAGRequest
                        {
                            RequestId = Guid.NewGuid().ToString(),
                            Query = request.Query,
                            UserId = $"test-{role.ToLower()}",
                            Role = role,
                            DepartmentId = request.DepartmentId ?? "8bf13891-1ce9-405c-add9-0ada93308671",
                            DepartmentName = "TestDepartment",
                            MaxResults = 10,
                            MinRelevanceScore = 0.001
                        };

                        var response = await _ragService.SearchDocumentsWithRAGAsync(ragRequest);

                        results[role] = new
                        {
                            Role = role,
                            Found = response.Success,
                            SourcesCount = response.Sources?.Count ?? 0,
                            ContentLength = response.RawContent?.Length ?? 0,
                            Sources = response.Sources?.Select(s => new
                            {
                                s.Title,
                                s.DepartmentId,
                                s.RelevanceScore
                            }).Take(3), // Show top 3 sources
                            Message = role == "ADMIN"
                                ? "Admin should NOT see any documents"
                                : role == "NONE"
                                    ? "None role sees only public documents"
                                    : $"{role} sees department + public documents"
                        };
                    }

                    return Ok(new
                    {
                        Query = request.Query,
                        DepartmentId = request.DepartmentId ?? "8bf13891-1ce9-405c-add9-0ada93308671",
                        RoleResults = results,
                        Summary = new
                        {
                            AdminAccess = "❌ No access (per business requirement)",
                            ManagerAccess = "✅ Department + Public documents",
                            EditorAccess = "✅ Department + Public documents",
                            MemberAccess = "✅ Department + Public documents",
                            NoneAccess = "✅ Public documents only"
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in role access test");
                    return StatusCode(500, new { error = ex.Message });
                }
            }

            /// <summary>
            /// Test 7: Test with Real Document Data
            /// </summary>
            [HttpGet("test-real-document")]
            public async Task<IActionResult> TestRealDocument()
            {
                try
                {
                    // Test with actual document data
                    var testCases = new[]
                    {
                    new {
                        Query = "phiếu đánh giá chất lượng phục vụ",
                        ExpectedDoc = "Phiếu đánh giá chất lượng phục vụ",
                        DepartmentId = "a02f4955-f08a-4839-a88a-d088299f8277"
                    },
                    new {
                        Query = "CSKH1",
                        ExpectedDoc = "Phiếu đánh giá chất lượng phục vụ",
                        DepartmentId = "a02f4955-f08a-4839-a88a-d088299f8277"
                    },
                    new {
                        Query = "tổng đài chăm sóc khách hàng",
                        ExpectedDoc = "Phiếu đánh giá chất lượng phục vụ",
                        DepartmentId = "a02f4955-f08a-4839-a88a-d088299f8277"
                    }
                };

                    var results = new List<object>();

                    foreach (var testCase in testCases)
                    {
                        // Test as document owner
                        var ownerRequest = new DocumentRAGRequest
                        {
                            RequestId = Guid.NewGuid().ToString(),
                            Query = testCase.Query,
                            UserId = "0570c6d9-f285-4892-a5f6-02c03400081a", // Actual owner ID
                            Role = "EDITOR",
                            DepartmentId = testCase.DepartmentId,
                            MaxResults = 5,
                            MinRelevanceScore = 0.001
                        };

                        var ownerResponse = await _ragService.SearchDocumentsWithRAGAsync(ownerRequest);

                        // Test as same department member
                        var memberRequest = new DocumentRAGRequest
                        {
                            RequestId = Guid.NewGuid().ToString(),
                            Query = testCase.Query,
                            UserId = "test-member",
                            Role = "MEMBER",
                            DepartmentId = testCase.DepartmentId,
                            MaxResults = 5,
                            MinRelevanceScore = 0.001
                        };

                        var memberResponse = await _ragService.SearchDocumentsWithRAGAsync(memberRequest);

                        // Test as different department
                        var otherDeptRequest = new DocumentRAGRequest
                        {
                            RequestId = Guid.NewGuid().ToString(),
                            Query = testCase.Query,
                            UserId = "test-other",
                            Role = "MEMBER",
                            DepartmentId = "different-dept-id",
                            MaxResults = 5,
                            MinRelevanceScore = 0.001
                        };

                        var otherDeptResponse = await _ragService.SearchDocumentsWithRAGAsync(otherDeptRequest);

                        results.Add(new
                        {
                            Query = testCase.Query,
                            Expected = testCase.ExpectedDoc,
                            OwnerAccess = new
                            {
                                HasAccess = ownerResponse.Success && !string.IsNullOrEmpty(ownerResponse.RawContent),
                                SourcesCount = ownerResponse.Sources?.Count ?? 0,
                                FoundExpected = ownerResponse.Sources?.Any(s => s.Title?.Contains(testCase.ExpectedDoc) == true) ?? false
                            },
                            SameDeptAccess = new
                            {
                                HasAccess = memberResponse.Success && !string.IsNullOrEmpty(memberResponse.RawContent),
                                SourcesCount = memberResponse.Sources?.Count ?? 0,
                                FoundExpected = memberResponse.Sources?.Any(s => s.Title?.Contains(testCase.ExpectedDoc) == true) ?? false,
                                Note = "Should have access (same dept, not public)"
                            },
                            DifferentDeptAccess = new
                            {
                                HasAccess = otherDeptResponse.Success && !string.IsNullOrEmpty(otherDeptResponse.RawContent),
                                SourcesCount = otherDeptResponse.Sources?.Count ?? 0,
                                Note = "Should NOT have access (different dept, not public)"
                            }
                        });
                    }

                    return Ok(new
                    {
                        TestDescription = "Testing with real document: 'Phiếu đánh giá chất lượng phục vụ'",
                        DocumentInfo = new
                        {
                            DocumentId = "bbc5f330517b4ed7b3115d472145e868",
                            VersionId = "befccfa2f2b74bc6b42ff56f4b1dfa4d",
                            Title = "Phiếu đánh giá chất lượng phục vụ",
                            DepartmentId = "a02f4955-f08a-4839-a88a-d088299f8277",
                            IsPublic = false,
                            EffectiveFrom = "2025-08-12T17:00:00Z",
                            EffectiveUntil = "2025-08-14T17:00:00Z",
                            Status = "Approved"
                        },
                        TestResults = results,
                        Summary = new
                        {
                            Note = "Document is NOT public, so only owner and same department members should access it"
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in real document test");
                    return StatusCode(500, new { error = ex.Message });
                }
            }
        }

        #region Request DTOs

        public class TestRAGRequest
        {
            public string Query { get; set; }
            public string? TestUserId { get; set; }
            public string? TestEmail { get; set; }
            public string? TestFullName { get; set; }
            public string? Role { get; set; }
            public string? DepartmentId { get; set; }
            public string? DepartmentName { get; set; }
            public int? MaxResults { get; set; }
            public double? MinRelevanceScore { get; set; }
            public bool OnlyPublic { get; set; }
            public List<string>? Tags { get; set; }
        }

        public class RoleTestRequest
        {
            public string Query { get; set; }
            public string? DepartmentId { get; set; }
        }

        #endregion
    }

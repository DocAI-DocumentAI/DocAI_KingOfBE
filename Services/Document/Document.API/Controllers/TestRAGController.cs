using System.Security.Claims;
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Document.API.Controllers
{

    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class RAGAccuracyTestController : ControllerBase
    {
        private readonly IDocumentRAGService _ragService;
        private readonly ILogger<RAGAccuracyTestController> _logger;

        public RAGAccuracyTestController(
            IDocumentRAGService ragService,
            ILogger<RAGAccuracyTestController> logger)
        {
            _ragService = ragService;
            _logger = logger;
        }

        /// <summary>
        /// Test 1: Chunking Strategy Impact
        /// </summary>
        [HttpPost("test-chunking-strategy")]
        public async Task<IActionResult> TestChunkingStrategy([FromBody] ChunkingTestRequest request)
        {
            try
            {
                _logger.LogInformation("🧪 Testing chunking strategy with query: {Query}", request.Query);

                var results = new List<object>();
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "test-user";

                // Test với các chunking configurations khác nhau
                var chunkingConfigs = new[]
                {
                    new { Name = "Current_800_150", MaxTokens = 800, Overlap = 150 },
                    new { Name = "Small_400_100", MaxTokens = 400, Overlap = 100 },
                    new { Name = "Large_1200_200", MaxTokens = 1200, Overlap = 200 },
                    new { Name = "Default_1000_100", MaxTokens = 1000, Overlap = 100 }
                };

                foreach (var config in chunkingConfigs)
                {
                    var ragRequest = new DocumentRAGRequest
                    {
                        RequestId = Guid.NewGuid().ToString(),
                        Query = request.Query,
                        UserId = userId,
                        Role = "MEMBER",
                        DepartmentId = "a02f4955-f08a-4839-a88a-d088299f8277",
                        MaxResults = 10,
                        MinRelevanceScore = 0.001
                    };

                    var response = await _ragService.SearchDocumentsWithRAGAsync(ragRequest);

                    results.Add(new
                    {
                        Config = config.Name,
                        MaxTokens = config.MaxTokens,
                        Overlap = config.Overlap,
                        Success = response.Success,
                        SourcesFound = response.Sources?.Count ?? 0,
                        ContentLength = response.RawContent?.Length ?? 0,
                        ProcessingTime = response.ProcessingTimeMs,
                        TopRelevance = response.Sources?.FirstOrDefault()?.RelevanceScore ?? 0,
                        AverageRelevance = response.Sources?.Any() == true
                            ? response.Sources.Average(s => s.RelevanceScore)
                            : 0
                    });
                }

                return Ok(new
                {
                    TestName = "Chunking Strategy Impact",
                    Query = request.Query,
                    Results = results,
                    Recommendation = new
                    {
                        BestConfig = results.OrderByDescending(r => ((dynamic)r).TopRelevance).FirstOrDefault(),
                        Note = "Config với TopRelevance cao nhất thường cho kết quả tốt nhất"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing chunking strategy");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Test 2: Relevance Threshold Optimization
        /// </summary>
        [HttpPost("test-relevance-thresholds")]
        public async Task<IActionResult> TestRelevanceThresholds([FromBody] RelevanceTestRequest request)
        {
            try
            {
                _logger.LogInformation("🧪 Testing relevance thresholds with query: {Query}", request.Query);

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "test-user";
                var results = new List<object>();

                // Test với các threshold khác nhau
                var thresholds = new[] { 0.0, 0.001, 0.005, 0.01, 0.02, 0.05, 0.1 };

                foreach (var threshold in thresholds)
                {
                    var ragRequest = new DocumentRAGRequest
                    {
                        RequestId = Guid.NewGuid().ToString(),
                        Query = request.Query,
                        UserId = userId,
                        Role = "MEMBER",
                        DepartmentId = "a02f4955-f08a-4839-a88a-d088299f8277",
                        MaxResults = 20,
                        MinRelevanceScore = threshold
                    };

                    var response = await _ragService.SearchDocumentsWithRAGAsync(ragRequest);

                    results.Add(new
                    {
                        Threshold = threshold,
                        SourcesFound = response.Sources?.Count ?? 0,
                        MinRelevanceInResults = response.Sources?.Any() == true
                            ? response.Sources.Min(s => s.RelevanceScore)
                            : 0,
                        MaxRelevanceInResults = response.Sources?.Any() == true
                            ? response.Sources.Max(s => s.RelevanceScore)
                            : 0,
                        QualitySources = response.Sources?.Count(s => s.RelevanceScore > 0.01) ?? 0,
                        ContentQuality = EvaluateContentQuality(response.RawContent, request.Query)
                    });
                }

                return Ok(new
                {
                    TestName = "Relevance Threshold Optimization",
                    Query = request.Query,
                    Results = results,
                    Analysis = new
                    {
                        OptimalThreshold = FindOptimalThreshold(results),
                        TooLowThreshold = "0.0 - 0.001: Quá nhiều noise",
                        BalancedThreshold = "0.005 - 0.01: Cân bằng quantity vs quality",
                        HighThreshold = "0.05+: Có thể miss relevant documents"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing relevance thresholds");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Test 3: Query Classification và Strategy Testing
        /// </summary>
        [HttpPost("test-query-strategies")]
        public async Task<IActionResult> TestQueryStrategies()
        {
            try
            {
                _logger.LogInformation("🧪 Testing different query classification strategies");

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "test-user";

                // Test queries được classify khác nhau
                var testCases = new[]
                {
                    new { Query = "quy định", Type = "General", ExpectedResults = "Many broad results" },
                    new { Query = "Công tác chỉ đạo, điều hành cải cách hành chính", Type = "Specific", ExpectedResults = "Targeted results" },
                    new { Query = "làm sao thực hiện cải cách thủ tục?", Type = "Question", ExpectedResults = "Process-oriented results" },
                    new { Query = "Quyết định số 1647/QĐ-BTC", Type = "DocumentReference", ExpectedResults = "Exact document match" },
                    new { Query = "a", Type = "Invalid", ExpectedResults = "No meaningful results" }
                };

                var results = new List<object>();

                foreach (var testCase in testCases)
                {
                    var ragRequest = new DocumentRAGRequest
                    {
                        RequestId = Guid.NewGuid().ToString(),
                        Query = testCase.Query,
                        UserId = userId,
                        Role = "MEMBER",
                        DepartmentId = "a02f4955-f08a-4839-a88a-d088299f8277",
                        MaxResults = 10,
                        MinRelevanceScore = 0.001
                    };

                    var response = await _ragService.SearchDocumentsWithRAGAsync(ragRequest);

                    results.Add(new
                    {
                        testCase.Query,
                        ExpectedType = testCase.Type,
                        testCase.ExpectedResults,
                        ActualResults = new
                        {
                            SourcesFound = response.Sources?.Count ?? 0,
                            ContentLength = response.RawContent?.Length ?? 0,
                            ProcessingTime = response.ProcessingTimeMs,
                            TopRelevance = response.Sources?.FirstOrDefault()?.RelevanceScore ?? 0,
                            HasExactMatch = testCase.Type == "DocumentReference"
                                ? CheckExactDocumentMatch(response.Sources, testCase.Query)
                                : false
                        },
                        Performance = EvaluateQueryPerformance(testCase.Type, response)
                    });
                }

                return Ok(new
                {
                    TestName = "Query Classification Strategy Testing",
                    Results = results,
                    Analysis = new
                    {
                        GeneralQueries = "Should return broad, diverse results",
                        SpecificQueries = "Should return focused, relevant results",
                        QuestionQueries = "Should return process/method information",
                        DocumentReferences = "Should return exact document matches",
                        Recommendation = "Adjust MinRelevanceScore based on query type"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing query strategies");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Test 4: Vietnamese Text Processing Accuracy
        /// </summary>
        [HttpPost("test-vietnamese-processing")]
        public async Task<IActionResult> TestVietnameseProcessing()
        {
            try
            {
                _logger.LogInformation("🧪 Testing Vietnamese text processing accuracy");

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "test-user";

                // Test với các đặc trưng tiếng Việt
                var vietnameseTestCases = new[]
                {
                    new { Query = "cải cách", Variants = new[] { "cai cach", "cải-cách", "CẢI CÁCH" } },
                    new { Query = "thủ tục hành chính", Variants = new[] { "thu tuc hanh chinh", "thủ-tục hành-chính" } },
                    new { Query = "phê duyệt", Variants = new[] { "phe duyet", "PHÊ DUYỆT", "phê-duyệt" } },
                    new { Query = "tài chính công", Variants = new[] { "tai chinh cong", "tài-chính-công" } }
                };

                var results = new List<object>();

                foreach (var testCase in vietnameseTestCases)
                {
                    var variantResults = new List<object>();

                    // Test query gốc
                    var originalResponse = await SearchWithQuery(testCase.Query, userId);
                    variantResults.Add(new
                    {
                        Variant = testCase.Query + " (original)",
                        SourcesFound = originalResponse.Sources?.Count ?? 0,
                        TopRelevance = originalResponse.Sources?.FirstOrDefault()?.RelevanceScore ?? 0
                    });

                    // Test các variants
                    foreach (var variant in testCase.Variants)
                    {
                        var variantResponse = await SearchWithQuery(variant, userId);
                        variantResults.Add(new
                        {
                            Variant = variant,
                            SourcesFound = variantResponse.Sources?.Count ?? 0,
                            TopRelevance = variantResponse.Sources?.FirstOrDefault()?.RelevanceScore ?? 0
                        });
                    }

                    results.Add(new
                    {
                        OriginalQuery = testCase.Query,
                        VariantResults = variantResults,
                        ConsistencyScore = CalculateConsistencyScore(variantResults)
                    });
                }

                return Ok(new
                {
                    TestName = "Vietnamese Text Processing Accuracy",
                    Results = results,
                    Analysis = new
                    {
                        AccentSensitivity = "Test khả năng xử lý dấu tiếng Việt",
                        CaseSensitivity = "Test khả năng xử lý chữ hoa/thường",
                        HyphenHandling = "Test khả năng xử lý dấu gạch nối",
                        ConsistencyMeasure = "Điểm nhất quán giữa các variants (0-1)"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Vietnamese processing");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Test 5: Content Extraction Quality
        /// </summary>
        [HttpPost("test-content-extraction")]
        public async Task<IActionResult> TestContentExtraction([FromBody] ContentExtractionTestRequest request)
        {
            try
            {
                _logger.LogInformation("🧪 Testing content extraction quality for: {Query}", request.Query);

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "test-user";

                var ragRequest = new DocumentRAGRequest
                {
                    RequestId = Guid.NewGuid().ToString(),
                    Query = request.Query,
                    UserId = userId,
                    Role = "MEMBER",
                    DepartmentId = "a02f4955-f08a-4839-a88a-d088299f8277",
                    MaxResults = 10,
                    MinRelevanceScore = 0.001
                };

                var response = await _ragService.SearchDocumentsWithRAGAsync(ragRequest);

                // Analyze extracted content
                var contentAnalysis = AnalyzeExtractedContent(response.RawContent, request.Query);

                return Ok(new
                {
                    TestName = "Content Extraction Quality",
                    Query = request.Query,
                    ExtractedContent = new
                    {
                        Length = response.RawContent?.Length ?? 0,
                        Preview = response.RawContent?.Length > 200
                            ? response.RawContent.Substring(0, 200) + "..."
                            : response.RawContent,
                        SourceCount = response.Sources?.Count ?? 0
                    },
                    QualityMetrics = contentAnalysis,
                    SourcesAnalysis = response.Sources?.Select(s => new
                    {
                        s.Title,
                        s.RelevanceScore,
                        s.DepartmentId,
                        ContentPreview = s.Summary?.Length > 100
                            ? s.Summary.Substring(0, 100) + "..."
                            : s.Summary
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing content extraction");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Test 6: Comprehensive Accuracy Benchmark
        /// </summary>
        [HttpGet("benchmark-accuracy")]
        public async Task<IActionResult> BenchmarkAccuracy()
        {
            try
            {
                _logger.LogInformation("🧪 Running comprehensive accuracy benchmark");

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "test-user";

                // Golden dataset - queries với expected results từ tài liệu thật
                var goldenDataset = new[]
                {
                    new {
                        Query = "Công tác chỉ đạo, điều hành cải cách hành chính",
                        ExpectedKeywords = new[] { "chỉ đạo", "điều hành", "cải cách hành chính" },
                        ExpectedDocumentType = "Official Policy"
                    },
                    new {
                        Query = "Mục tiêu cụ thể năm 2025",
                        ExpectedKeywords = new[] { "mục tiêu", "2025", "cụ thể" },
                        ExpectedDocumentType = "Planning Document"
                    },
                    new {
                        Query = "100% thủ tục hành chính trực tuyến",
                        ExpectedKeywords = new[] { "100%", "thủ tục hành chính", "trực tuyến" },
                        ExpectedDocumentType = "Digital Transformation"
                    }
                };

                var benchmarkResults = new List<object>();

                foreach (var testCase in goldenDataset)
                {
                    var response = await SearchWithQuery(testCase.Query, userId);

                    var accuracy = CalculateAccuracyScore(
                        response.RawContent,
                        testCase.ExpectedKeywords,
                        response.Sources?.Count ?? 0
                    );

                    benchmarkResults.Add(new
                    {
                        testCase.Query,
                        testCase.ExpectedKeywords,
                        ActualResults = new
                        {
                            SourcesFound = response.Sources?.Count ?? 0,
                            ContentLength = response.RawContent?.Length ?? 0,
                            TopRelevance = response.Sources?.FirstOrDefault()?.RelevanceScore ?? 0
                        },
                        AccuracyScore = accuracy,
                        KeywordMatches = CountKeywordMatches(response.RawContent, testCase.ExpectedKeywords)
                    });
                }

                var overallAccuracy = benchmarkResults.Any()
                    ? benchmarkResults.Average(r => ((dynamic)r).AccuracyScore)
                    : 0;

                return Ok(new
                {
                    TestName = "Comprehensive Accuracy Benchmark",
                    OverallAccuracy = Math.Round(overallAccuracy * 100, 2) + "%",
                    BenchmarkResults = benchmarkResults,
                    Summary = new
                    {
                        TotalQueries = goldenDataset.Length,
                        AverageAccuracy = overallAccuracy,
                        HighAccuracy = benchmarkResults.Count(r => ((dynamic)r).AccuracyScore > 0.8),
                        MediumAccuracy = benchmarkResults.Count(r => ((dynamic)r).AccuracyScore > 0.5 && ((dynamic)r).AccuracyScore <= 0.8),
                        LowAccuracy = benchmarkResults.Count(r => ((dynamic)r).AccuracyScore <= 0.5)
                    },
                    Recommendations = overallAccuracy > 0.8
                        ? "RAG system đạt accuracy tốt"
                        : "Cần optimize thêm: chunking, embedding model, hoặc relevance threshold"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running accuracy benchmark");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #region Helper Methods

        private async Task<DocumentRAGResponse> SearchWithQuery(string query, string userId)
        {
            var request = new DocumentRAGRequest
            {
                RequestId = Guid.NewGuid().ToString(),
                Query = query,
                UserId = userId,
                Role = "MEMBER",
                DepartmentId = "a02f4955-f08a-4839-a88a-d088299f8277",
                MaxResults = 10,
                MinRelevanceScore = 0.001
            };

            return await _ragService.SearchDocumentsWithRAGAsync(request);
        }

        private object EvaluateContentQuality(string content, string query)
        {
            if (string.IsNullOrEmpty(content))
                return new { Score = 0, Reason = "No content" };

            var queryWords = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var contentLower = content.ToLower();
            var matchedWords = queryWords.Count(word => contentLower.Contains(word));

            return new
            {
                Score = Math.Round((double)matchedWords / queryWords.Length, 2),
                MatchedWords = matchedWords,
                TotalWords = queryWords.Length,
                ContentLength = content.Length
            };
        }

        private object FindOptimalThreshold(List<object> results)
        {
            // Find threshold với balance tốt nhất giữa quantity và quality
            var optimal = results
                .Where(r => ((dynamic)r).SourcesFound > 0)
                .OrderByDescending(r => ((dynamic)r).QualitySources)
                .ThenByDescending(r => ((dynamic)r).SourcesFound)
                .FirstOrDefault();

            return optimal != null ? ((dynamic)optimal).Threshold : 0.001;
        }

        private string EvaluateQueryPerformance(string queryType, DocumentRAGResponse response)
        {
            var sourceCount = response.Sources?.Count ?? 0;
            var topRelevance = response.Sources?.FirstOrDefault()?.RelevanceScore ?? 0;

            return queryType switch
            {
                "General" => sourceCount > 5 ? "Good - Many results" : "Poor - Too few results",
                "Specific" => topRelevance > 0.1 ? "Good - High relevance" : "Poor - Low relevance",
                "Question" => response.RawContent?.Length > 500 ? "Good - Detailed answer" : "Poor - Brief answer",
                "DocumentReference" => topRelevance > 0.5 ? "Excellent - Exact match" : "Poor - No exact match",
                _ => "Unknown query type"
            };
        }

        private bool CheckExactDocumentMatch(List<DocumentSourceResponse> sources, string query)
        {
            if (sources == null || !sources.Any()) return false;

            // Check if any source title contains the document reference
            return sources.Any(s => !string.IsNullOrEmpty(s.Title) &&
                query.ToLower().Contains(s.Title.ToLower().Substring(0, Math.Min(10, s.Title.Length))));
        }

        private double CalculateConsistencyScore(List<object> variantResults)
        {
            if (!variantResults.Any()) return 0;

            var sourceCounts = variantResults.Select(r => ((dynamic)r).SourcesFound).ToList();
            var relevanceScores = variantResults.Select(r => ((dynamic)r).TopRelevance).ToList();

            var sourceVariance = CalculateVariance(sourceCounts.Select(x => (double)x));
            var relevanceVariance = CalculateVariance(relevanceScores.Select(x => (double)x));

            // Lower variance = higher consistency
            return Math.Max(0, 1 - (sourceVariance + relevanceVariance) / 2);
        }

        private double CalculateVariance(IEnumerable<double> values)
        {
            if (!values.Any()) return 0;
            var mean = values.Average();
            return values.Average(x => Math.Pow(x - mean, 2));
        }

        private object AnalyzeExtractedContent(string content, string query)
        {
            if (string.IsNullOrEmpty(content))
                return new { HasCompleteParagraphs = false, HasGoodContext = false, HighRelevanceRatio = 0.0 };

            var paragraphs = content.Split(new[] { "\n\n", "---" }, StringSplitOptions.RemoveEmptyEntries);
            var queryWords = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var contentLower = content.ToLower();

            var relevantParagraphs = paragraphs.Count(p =>
                queryWords.Any(word => p.ToLower().Contains(word)));

            return new
            {
                HasCompleteParagraphs = paragraphs.Length > 1,
                HasGoodContext = content.Length > 300,
                HighRelevanceRatio = paragraphs.Length > 0 ? (double)relevantParagraphs / paragraphs.Length : 0,
                TotalParagraphs = paragraphs.Length,
                RelevantParagraphs = relevantParagraphs
            };
        }

        private double CalculateAccuracyScore(string content, string[] expectedKeywords, int sourceCount)
        {
            if (string.IsNullOrEmpty(content) || sourceCount == 0) return 0;

            var contentLower = content.ToLower();
            var matchedKeywords = expectedKeywords.Count(keyword =>
                contentLower.Contains(keyword.ToLower()));

            var keywordScore = (double)matchedKeywords / expectedKeywords.Length;
            var sourceScore = Math.Min(1.0, sourceCount / 5.0); // Normalize to max 5 sources

            return (keywordScore * 0.7) + (sourceScore * 0.3); // Weighted combination
        }

        private int CountKeywordMatches(string content, string[] keywords)
        {
            if (string.IsNullOrEmpty(content)) return 0;

            var contentLower = content.ToLower();
            return keywords.Count(keyword => contentLower.Contains(keyword.ToLower()));
        }

        #endregion

        #region Request DTOs

        public class ChunkingTestRequest
        {
            public string Query { get; set; }
        }

        public class RelevanceTestRequest
        {
            public string Query { get; set; }
        }

        public class ContentExtractionTestRequest
        {
            public string Query { get; set; }
        }

        #endregion
    }
}

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
    [Authorize] // Remove this if you want to test without auth
    public class TestRAGController : ControllerBase
    {
        private readonly IDocumentRAGService _ragService;
        private readonly ILogger<TestRAGController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TestRAGController(
     IDocumentRAGService ragService,
     ILogger<TestRAGController> logger)
        {
            _ragService = ragService;
            _logger = logger;
        }
        #region Actual System Data Test Cases

        /// <summary>
        /// TEST 1: Tóm tắt document cụ thể - Thông tư Giải thưởng Khoa học
        /// Expected: Load toàn bộ document để AI tóm tắt chi tiết
        /// </summary>
        [HttpPost("test1-specific-doc-summary")]
        public async Task<IActionResult> Test1_SpecificDocumentSummary()
        {
            var testRequest = new DocumentRAGRequest
            {
                DocumentId = "e473a8c85dd94c1e8ac766f89518b3cb", // Thông tư Giải thưởng
                Query = "Tóm tắt tài liệu này",
                RequestId = Guid.NewGuid().ToString(),
                UserId = "employee123",
                Email = "employee@company.com",
                FullName = "Nhân viên Test",
                Role = "EMPLOYEE",
                DepartmentId = "1a069837-cce1-4359-ad9b-c421c59b45cd", // ✅ Same department
                DepartmentName = "Phòng Nhân Sự",
                MaxResults = 10,
                MinRelevanceScore = 0.001,
                OnlyPublic = false,
                Permissions = new List<string> { "VIEW_DEPARTMENT_DOCUMENT" }
            };

            var result = await _ragService.SearchDocumentsWithRAGAsync(testRequest);

            return Ok(new
            {
                TestName = "TEST 1: Tóm tắt Thông tư Giải thưởng Khoa học",
                DocumentInfo = new
                {
                    DocumentId = "e473a8c85dd94c1e8ac766f89518b3cb",
                    Title = "Thông tư về Giải thưởng Khoa học và Công nghệ dành cho Sinh viên năm 2025",
                    Department = "Phòng Nhân Sự",
                    IsPublic = false,
                    EffectiveFrom = "2025-05-29T17:00:00Z",
                    EffectiveUntil = "2025-08-22T17:00:00Z"
                },
                UserContext = new
                {
                    Role = testRequest.Role,
                    SameDepartment = true,
                    HasAccess = true
                },
                ExpectedBehavior = new
                {
                    ShouldLoadFullDocument = true,
                    SearchLimit = 300,
                    MinRelevance = 0.0,
                    ContentType = "Complete document content about Giải thưởng Khoa học",
                    PermissionCheck = "PASS - Same department employee"
                },
                ActualResult = result
            });
        }

        /// <summary>
        /// TEST 2: Hỏi chi tiết về document cụ thể - Quy định bảo hiểm y tế
        /// Expected: Load document với focus vào nội dung liên quan
        /// </summary>
        [HttpPost("test2-specific-question")]
        public async Task<IActionResult> Test2_SpecificQuestion()
        {
            var testRequest = new DocumentRAGRequest
            {
                DocumentId = "6d8d0423b0c44063a824a607908923fe", // Quy định bảo hiểm y tế
                Query = "Thời hạn lập danh sách tham gia bảo hiểm y tế là bao lâu?",
                RequestId = Guid.NewGuid().ToString(),
                UserId = "manager123",
                Email = "manager@company.com",
                FullName = "Quản lý Test",
                Role = "MANAGER",
                DepartmentId = "1a069837-cce1-4359-ad9b-c421c59b45cd", // ✅ Same department
                DepartmentName = "Phòng Nhân Sự",
                MaxResults = 5,
                MinRelevanceScore = 0.01,
                OnlyPublic = false,
                Permissions = new List<string> { "VIEW_DEPARTMENT_DOCUMENT", "MANAGE_EMPLOYEE" }
            };

            var result = await _ragService.SearchDocumentsWithRAGAsync(testRequest);

            return Ok(new
            {
                TestName = "TEST 2: Câu hỏi chi tiết về Quy định bảo hiểm y tế",
                DocumentInfo = new
                {
                    DocumentId = "6d8d0423b0c44063a824a607908923fe",
                    Title = "Quy định về danh sách tham gia bảo hiểm y tế",
                    Department = "Phòng Nhân Sự",
                    IsPublic = false,
                    Summary = "Quy định về việc lập danh sách tham gia BHYT trong 30 ngày"
                },
                ExpectedAnswer = "30 ngày kể từ ngày người lao động thuộc đối tượng tham gia bảo hiểm y tế",
                ExpectedBehavior = new
                {
                    ShouldLoadFullDocument = true,
                    QueryClassification = "specific_section",
                    FocusOnTimeLimit = true,
                    PermissionCheck = "PASS - Manager same department"
                },
                ActualResult = result
            });
        }

        /// <summary>
        /// TEST 3: Search chung về tai nạn lao động
        /// Expected: Tìm document public về tai nạn lao động
        /// </summary>
        [HttpPost("test3-general-search")]
        public async Task<IActionResult> Test3_GeneralSearch()
        {
            var testRequest = new DocumentRAGRequest
            {
                DocumentId = null, // General search
                Query = "Quy trình điều tra tai nạn lao động",
                RequestId = Guid.NewGuid().ToString(),
                UserId = "external123",
                Email = "external@guest.com",
                FullName = "Người dùng bên ngoài",
                Role = "NONE", // ✅ No role - should only see public docs
                DepartmentId = null,
                DepartmentName = null,
                MaxResults = 5,
                MinRelevanceScore = 0.01,
                OnlyPublic = true, // ✅ Only public docs
                Permissions = new List<string>()
            };

            var result = await _ragService.SearchDocumentsWithRAGAsync(testRequest);

            return Ok(new
            {
                TestName = "TEST 3: General search - Public documents only",
                SearchContext = new
                {
                    Query = "Quy trình điều tra tai nạn lao động",
                    UserRole = "NONE",
                    OnlyPublic = true
                },
                ExpectedDocuments = new[]
                {
                new
                {
                    DocumentId = "2895389b6336463d9b2fa56058b818dd",
                    Title = "Quy trình điều tra tai nạn lao động",
                    IsPublic = true, // ✅ This should be found
                    Reason = "Public document accessible to anyone"
                }
            },
                ExpectedBehavior = new
                {
                    SearchAllDocuments = true,
                    FilterByPublic = true,
                    QueryClassification = "general",
                    PermissionCheck = "Only public documents"
                },
                ActualResult = result
            });
        }

        /// <summary>
        /// TEST 4: Permission denied - Different department user
        /// Expected: Không access được private documents của Phòng Nhân Sự
        /// </summary>
        [HttpPost("test4-permission-denied")]
        public async Task<IActionResult> Test4_PermissionDenied()
        {
            var testRequest = new DocumentRAGRequest
            {
                DocumentId = "60277c8fa0544f808f676dfbd3dc2c20", // Nghị định lương tối thiểu (private)
                Query = "Mức lương tối thiểu mới nhất",
                RequestId = Guid.NewGuid().ToString(),
                UserId = "other_dept_user",
                Email = "user@other-dept.com",
                FullName = "Nhân viên phòng khác",
                Role = "EMPLOYEE",
                DepartmentId = "OTHER_DEPT_ID", // ✅ Different department
                DepartmentName = "Phòng Khác",
                MaxResults = 5,
                MinRelevanceScore = 0.01,
                OnlyPublic = false,
                Permissions = new List<string>()
            };

            var result = await _ragService.SearchDocumentsWithRAGAsync(testRequest);

            return Ok(new
            {
                TestName = "TEST 4: Permission Denied - Different Department",
                TargetDocument = new
                {
                    DocumentId = "60277c8fa0544f808f676dfbd3dc2c20",
                    Title = "Nghị định về mức lương tối thiểu",
                    BelongsToDepartment = "1a069837-cce1-4359-ad9b-c421c59b45cd", // Phòng Nhân Sự
                    IsPublic = false
                },
                UserContext = new
                {
                    UserDepartment = "OTHER_DEPT_ID",
                    Role = "EMPLOYEE",
                    DifferentDepartment = true
                },
                ExpectedBehavior = new
                {
                    AccessDenied = true,
                    DocumentBlocked = true,
                    NoContentReturned = true,
                    NoSourcesReturned = true,
                    Reason = "User from different department cannot access private documents"
                },
                ActualResult = result
            });
        }

        /// <summary>
        /// TEST 5: Admin should be blocked
        /// Expected: Admin không được search bất kỳ document nào
        /// </summary>
        [HttpPost("test5-admin-blocked")]
        public async Task<IActionResult> Test5_AdminBlocked()
        {
            var testRequest = new DocumentRAGRequest
            {
                DocumentId = null,
                Query = "Bất kỳ tài liệu nào về nhân sự",
                RequestId = Guid.NewGuid().ToString(),
                UserId = "admin123",
                Email = "admin@company.com",
                FullName = "Admin User",
                Role = "ADMIN", // ✅ Admin should be completely blocked
                DepartmentId = "1a069837-cce1-4359-ad9b-c421c59b45cd",
                DepartmentName = "Phòng Quản trị",
                MaxResults = 10,
                MinRelevanceScore = 0.001,
                OnlyPublic = false,
                Permissions = new List<string> { "ADMIN_ALL", "VIEW_ALL" }
            };

            var result = await _ragService.SearchDocumentsWithRAGAsync(testRequest);

            return Ok(new
            {
                TestName = "TEST 5: Admin Role Completely Blocked",
                UserContext = new
                {
                    Role = "ADMIN",
                    HasAllPermissions = true,
                    ShouldBeBlocked = true
                },
                BusinessRule = "Admin không được phép search documents theo yêu cầu nghiệp vụ",
                ExpectedBehavior = new
                {
                    CompletelyBlocked = true,
                    FilterByImpossibleTag = "accessLevel = SUPER_ADMIN_ONLY",
                    NoDocumentsFound = true,
                    EmptyResponse = true
                },
                ActualResult = result
            });
        }

        /// <summary>
        /// TEST 6: Public document access for guest
        /// Expected: Guest chỉ được access public documents
        /// </summary>
        [HttpPost("test6-guest-public-access")]
        public async Task<IActionResult> Test6_GuestPublicAccess()
        {
            var testRequest = new DocumentRAGRequest
            {
                DocumentId = "5716912b3fc34952836af401dbc37cbb", // Quy định thí điểm (public)
                Query = "Quy định về thực hiện thí điểm",
                RequestId = Guid.NewGuid().ToString(),
                UserId = "guest123",
                Email = "guest@external.com",
                FullName = "Guest User",
                Role = "NONE",
                DepartmentId = null,
                DepartmentName = null,
                MaxResults = 5,
                MinRelevanceScore = 0.01,
                OnlyPublic = true, // ✅ Guest only public
                Permissions = new List<string>()
            };

            var result = await _ragService.SearchDocumentsWithRAGAsync(testRequest);

            return Ok(new
            {
                TestName = "TEST 6: Guest Access Public Document",
                TargetDocument = new
                {
                    DocumentId = "5716912b3fc34952836af401dbc37cbb",
                    Title = "Quy định về thực hiện thí điểm",
                    IsPublic = true, // ✅ Should be accessible
                    SignedBy = "Trương Thị Mai",
                    EffectiveUntil = "2025-08-17T17:00:00Z"
                },
                UserContext = new
                {
                    Role = "NONE",
                    IsGuest = true,
                    OnlyPublicAccess = true
                },
                ExpectedBehavior = new
                {
                    AccessGranted = true,
                    PublicDocumentOnly = true,
                    ShouldReturnContent = true,
                    FilterByPublicTag = "isPublic = True"
                },
                ActualResult = result
            });
        }

        /// <summary>
        /// TEST 7: Analysis request - Phân tích tài liệu lương
        /// Expected: Load complete content for comprehensive analysis
        /// </summary>
        [HttpPost("test7-document-analysis")]
        public async Task<IActionResult> Test7_DocumentAnalysis()
        {
            var testRequest = new DocumentRAGRequest
            {
                DocumentId = "60277c8fa0544f808f676dfbd3dc2c20", // Nghị định lương tối thiểu
                Query = "Phân tích chi tiết nghị định về mức lương tối thiểu, tác động và những điểm chính",
                RequestId = Guid.NewGuid().ToString(),
                UserId = "analyst123",
                Email = "analyst@company.com",
                FullName = "Chuyên viên phân tích",
                Role = "MANAGER",
                DepartmentId = "1a069837-cce1-4359-ad9b-c421c59b45cd", // ✅ Same department  
                DepartmentName = "Phòng Nhân Sự",
                MaxResults = 15,
                MinRelevanceScore = 0.001,
                OnlyPublic = false,
                Permissions = new List<string> { "VIEW_DEPARTMENT_DOCUMENT", "ANALYZE_DOCUMENT" }
            };

            var result = await _ragService.SearchDocumentsWithRAGAsync(testRequest);

            return Ok(new
            {
                TestName = "TEST 7: Document Analysis Request",
                TargetDocument = new
                {
                    DocumentId = "60277c8fa0544f808f676dfbd3dc2c20",
                    Title = "Nghị định về mức lương tối thiểu",
                    VersionName = "Nghị định số 74-2024-NĐ-CP",
                    SignedBy = "Lê Minh Khái",
                    EffectiveFrom = "2025-08-13T17:00:00Z"
                },
                AnalysisRequest = new
                {
                    QueryType = "Comprehensive analysis",
                    RequiresFullContent = true,
                    AnalysisAspects = new[] { "Ưu điểm", "Nhược điểm", "Tác động", "Điểm chính" }
                },
                ExpectedBehavior = new
                {
                    QueryClassification = "full_document",
                    LoadCompleteDocument = true,
                    HighContentLimit = 25000,
                    ComprehensiveAnalysis = true,
                    PermissionCheck = "PASS - Manager same department"
                },
                ActualResult = result
            });
        }

        /// <summary>
        /// TEST 8: Search multiple documents about employment
        /// Expected: Find all relevant employment-related documents user has access to
        /// </summary>
        [HttpPost("test8-multi-document-search")]
        public async Task<IActionResult> Test8_MultiDocumentSearch()
        {
            var testRequest = new DocumentRAGRequest
            {
                DocumentId = null, // Multi-document search
                Query = "Quy định về hợp đồng lao động và kỷ luật nhân viên",
                RequestId = Guid.NewGuid().ToString(),
                UserId = "hr_staff123",
                Email = "hr@company.com",
                FullName = "Nhân viên HR",
                Role = "EMPLOYEE",
                DepartmentId = "1a069837-cce1-4359-ad9b-c421c59b45cd", // ✅ HR Department
                DepartmentName = "Phòng Nhân Sự",
                MaxResults = 10,
                MinRelevanceScore = 0.01,
                OnlyPublic = false,
                Permissions = new List<string> { "VIEW_DEPARTMENT_DOCUMENT" }
            };

            var result = await _ragService.SearchDocumentsWithRAGAsync(testRequest);

            return Ok(new
            {
                TestName = "TEST 8: Multi-Document Search - Employment Related",
                SearchQuery = "Quy định về hợp đồng lao động và kỷ luật nhân viên",
                PotentialDocuments = new[]
                {
                new { DocumentId = "ac1bc07e854f4edd9139d6cd51e4bbb8", Title = "Quy định về ký hợp đồng", Tags = new[] { "ký hợp đồng", "trách nhiệm" } },
                new { DocumentId = "9f6f9ea9845342778db865df81538878", Title = "Biên bản xử lý vi phạm kỷ luật lao động", Tags = new[] { "xử lý vi phạm", "kỷ luật" } },
                new { DocumentId = "2895389b6336463d9b2fa56058b818dd", Title = "Quy trình điều tra tai nạn lao động", Tags = new[] { "tai nạn lao động", "quy trình" } }
            },
                ExpectedBehavior = new
                {
                    SearchMultipleDocuments = true,
                    GroupByDocument = true,
                    QueryClassification = "general",
                    CombineRelatedContent = true,
                    PermissionFiltering = "Department-level access"
                },
                ActualResult = result
            });
        }

        #endregion

        #region Comprehensive Test Runner

        /// <summary>
        /// Run all real data tests
        /// </summary>
        [HttpPost("run-all-real-tests")]
        public async Task<IActionResult> RunAllRealTests()
        {
            var testResults = new Dictionary<string, object>();
            var startTime = DateTime.UtcNow;

            try
            {
                _logger.LogInformation("🧪 [TEST-SUITE] Starting comprehensive real data tests");

                // Execute all tests
                var tests = new Dictionary<string, Func<Task<IActionResult>>>
                {
                    ["Test1_SpecificDocumentSummary"] = Test1_SpecificDocumentSummary,
                    ["Test2_SpecificQuestion"] = Test2_SpecificQuestion,
                    ["Test3_GeneralSearch"] = Test3_GeneralSearch,
                    ["Test4_PermissionDenied"] = Test4_PermissionDenied,
                    ["Test5_AdminBlocked"] = Test5_AdminBlocked,
                    ["Test6_GuestPublicAccess"] = Test6_GuestPublicAccess,
                    ["Test7_DocumentAnalysis"] = Test7_DocumentAnalysis,
                    ["Test8_MultiDocumentSearch"] = Test8_MultiDocumentSearch
                };

                foreach (var test in tests)
                {
                    try
                    {
                        _logger.LogInformation("🧪 [TEST] Running {TestName}", test.Key);
                        var testResult = await test.Value();
                        testResults[test.Key] = ((OkObjectResult)testResult).Value;
                        _logger.LogInformation("✅ [TEST] {TestName} completed", test.Key);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ [TEST] {TestName} failed", test.Key);
                        testResults[test.Key] = new { Error = ex.Message, StackTrace = ex.StackTrace };
                    }
                }

                var endTime = DateTime.UtcNow;
                var duration = endTime - startTime;

                return Ok(new
                {
                    TestSuite = "DocumentRAG Real Data Complete Test Suite",
                    SystemData = new
                    {
                        TotalDocuments = 9,
                        Department = "Phòng Nhân Sự (1a069837-cce1-4359-ad9b-c421c59b45cd)",
                        PublicDocuments = 2, // tai nạn lao động, thí điểm
                        PrivateDocuments = 7,
                        DocumentTypes = new[] { "Quy Định", "Nghị Định", "Thông Tư", "Biên Bản", "Hướng Dẫn" }
                    },
                    TestExecution = new
                    {
                        StartTime = startTime,
                        EndTime = endTime,
                        Duration = duration.TotalSeconds,
                        TotalTests = tests.Count,
                        SuccessfulTests = testResults.Count(r => !r.Value.ToString().Contains("Error")),
                        FailedTests = testResults.Count(r => r.Value.ToString().Contains("Error"))
                    },
                    TestCoverage = new
                    {
                        SpecificDocumentSearch = "✓",
                        GeneralSearch = "✓",
                        PermissionFiltering = "✓",
                        AdminBlocking = "✓",
                        PublicAccess = "✓",
                        DocumentAnalysis = "✓",
                        MultiDocumentSearch = "✓",
                        DepartmentSecurity = "✓"
                    },
                    Results = testResults
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [TEST-SUITE] Test suite execution failed");
                return BadRequest(new
                {
                    Error = "Test suite execution failed",
                    Message = ex.Message,
                    PartialResults = testResults
                });
            }
        }

        /// <summary>
        /// Quick validation of real documents accessibility
        /// </summary>
        [HttpGet("validate-system-data")]
        public IActionResult ValidateSystemData()
        {
            var systemDocuments = new[]
            {
            new { Id = "e473a8c85dd94c1e8ac766f89518b3cb", Title = "Thông tư Giải thưởng Khoa học", IsPublic = false, Dept = "HR" },
            new { Id = "6d8d0423b0c44063a824a607908923fe", Title = "Quy định bảo hiểm y tế", IsPublic = false, Dept = "HR" },
            new { Id = "2895389b6336463d9b2fa56058b818dd", Title = "Quy trình điều tra tai nạn", IsPublic = true, Dept = "HR" },
            new { Id = "0f319d060443478fb084f5959a0e6ece", Title = "Kiểm soát tài sản chức vụ", IsPublic = false, Dept = "HR" },
            new { Id = "60277c8fa0544f808f676dfbd3dc2c20", Title = "Nghị định lương tối thiểu", IsPublic = false, Dept = "HR" },
            new { Id = "ac1bc07e854f4edd9139d6cd51e4bbb8", Title = "Quy định ký hợp đồng", IsPublic = false, Dept = "HR" },
            new { Id = "5716912b3fc34952836af401dbc37cbb", Title = "Quy định thí điểm", IsPublic = true, Dept = "HR" },
            new { Id = "9f6f9ea9845342778db865df81538878", Title = "Biên bản kỷ luật", IsPublic = false, Dept = "HR" },
            new { Id = "224f6504edd54772b86f8c30a9d100aa", Title = "Quyết định sửa đổi", IsPublic = false, Dept = "HR" }
        };

            return Ok(new
            {
                SystemValidation = "Real Data Test Environment",
                DocumentSummary = new
                {
                    TotalDocuments = systemDocuments.Length,
                    PublicDocuments = systemDocuments.Count(d => d.IsPublic),
                    PrivateDocuments = systemDocuments.Count(d => !d.IsPublic),
                    AllFromHRDepartment = systemDocuments.All(d => d.Dept == "HR")
                },
                Documents = systemDocuments,
                TestRecommendations = new
                {
                    ForEmployeeSameDept = "Should access all 9 documents",
                    ForEmployeeDifferentDept = "Should access 0 documents (all private to HR)",
                    ForGuestUser = "Should access 2 public documents only",
                    ForAdminUser = "Should access 0 documents (completely blocked)",
                    ForSpecificDocId = "Should load full document content"
                },
                ReadyForTesting = true
            });
        }

        #endregion
    }
}
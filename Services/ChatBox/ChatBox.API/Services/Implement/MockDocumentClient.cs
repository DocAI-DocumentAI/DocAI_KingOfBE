using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;
using ChatBox.API.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace ChatBox.API.Services.Implement
{
    public class MockDocumentClient : IDocumentClient
    {
        private readonly ILogger<MockDocumentClient> _logger;

        private static readonly List<RelevantSourceResponseExternal> _mockDocuments = new List<RelevantSourceResponseExternal>
        {
            new RelevantSourceResponseExternal
            {
                FileName = "ChinhSachNghiPhep.pdf",
                TextSnippet = "Theo chính sách nghỉ phép của công ty, nhân viên có thể nộp đơn xin nghỉ phép hàng năm với tối thiểu 5 ngày làm việc trước ngày nghỉ dự kiến. Mỗi nhân viên có 15 ngày nghỉ phép hưởng lương mỗi năm.",
                Relevance = 0.95,
                SourceUrl = "http://mock-docs.com/chinhsach_nghi_phep.pdf"
            },
            new RelevantSourceResponseExternal
            {
                FileName = "QuyDinhLamViecTuXa.docx",
                TextSnippet = "Nhân viên có thể làm việc từ xa tối đa 2 ngày mỗi tuần. Yêu cầu làm việc từ xa cần được sự đồng ý của quản lý trực tiếp và nộp form online.",
                Relevance = 0.88,
                SourceUrl = "http://mock-docs.com/quy_dinh_lam_viec_tu_xa.docx"
            },
            new RelevantSourceResponseExternal
            {
                FileName = "HuongDanBaoCaoCongViec.pdf",
                TextSnippet = "Tất cả nhân viên phải nộp báo cáo công việc hàng tuần vào cuối ngày thứ Sáu. Báo cáo cần chi tiết về tiến độ dự án và các vấn đề phát sinh.",
                Relevance = 0.75,
                SourceUrl = "http://mock-docs.com/bao_cao_cong_viec.pdf"
            },
            new RelevantSourceResponseExternal
            {
                FileName = "TaiLieuMatAnNinh.pdf",
                TextSnippet = "Đây là thông tin bảo mật nội bộ chỉ dành cho ban giám đốc.",
                Relevance = 0.99,
                SourceUrl = "http://mock-docs.com/tai_lieu_mat.pdf"
            },
             new RelevantSourceResponseExternal
            {
                FileName = "TaiLieuHetHieuLuc.pdf",
                TextSnippet = "Tài liệu này đã hết hiệu lực từ ngày 01/01/2025 và không còn được áp dụng.",
                Relevance = 0.60,
                SourceUrl = "http://mock-docs.com/het_hieu_luc.pdf"
            }
        };

        public MockDocumentClient(ILogger<MockDocumentClient> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.LogWarning("WARNING: Using MockDocumentClient for Document Microservice. This is for testing only. NO REAL HTTP CALLS WILL BE MADE.");
        }

        public Task<SearchDocumentResponseExternal> SearchRelevantDocumentsAsync(SearchDocumentRequestExternal request)
        {
            _logger.LogInformation($"Mock Document Search received query: {request.Query?.Substring(0, Math.Min(request.Query.Length, 100))}. Applying mock filters.");
            int? limit = 5; 
            var filteredDocuments = _mockDocuments.Where(doc =>
            {
                bool matchesAllFilters = true;

                // Giả lập logic lọc dựa trên Filters string (user ID và status)
                if (request.Filters != null)
                {
                    foreach (var filter in request.Filters)
                    {
                        // Giả lập filter "user:<userId>"
                        if (filter.StartsWith("user:"))
                        {
                            string userIdFromFilter = filter.Split(':')[1];
                            if (doc.FileName == "TaiLieuMatAnNinh.pdf")
                            {
                                if (userIdFromFilter != "special_user_id_123") // User ID đặc biệt để thấy tài liệu mật
                                {
                                    matchesAllFilters = false;
                                    break;
                                }
                            }
                            // Giả lập: các tài liệu không mật và không hết hiệu lực hiển thị cho mọi user
                            else if (doc.FileName != "TaiLieuHetHieuLuc.pdf")
                            {
                                // Không cần lọc thêm theo user ID cho các tài liệu chung
                            }
                        }
                        // Giả lập filter "status:active"
                        else if (filter == "status:active")
                        {
                            // Giả lập: "TaiLieuHetHieuLuc.pdf" luôn bị coi là hết hiệu lực
                            if (doc.FileName == "TaiLieuHetHieuLuc.pdf")
                            {
                                matchesAllFilters = false;
                                break;
                            }
                            // Giả lập cho ChinhSachNghiPhep.pdf hết hạn sau 2025-01-01 (nếu có filter status:active)
                            // if (doc.FileName == "ChinhSachNghiPhep.pdf" && DateTime.UtcNow > new DateTime(2025, 1, 1)) { matchesAllFilters = false; break; }
                        }
                    }
                }

                // Luôn kiểm tra độ liên quan của request cuối cùng
                if (doc.Relevance < request.MinRelevance)
                {
                    matchesAllFilters = false;
                }

                return matchesAllFilters;
            })
            .OrderByDescending(doc => doc.Relevance)
            .Take(limit ?? _mockDocuments.Count) // Sử dụng Limit từ request nếu có
            .ToList();

            var response = new SearchDocumentResponseExternal
            {
                Query = request.Query,
                RelevantSources = filteredDocuments,
                NoResult = !filteredDocuments.Any(),
                Answer = ""
            };

            if (response.NoResult)
            {
                _logger.LogInformation("Mock Document Search returned no relevant documents after filtering.");
            }
            else
            {
                _logger.LogInformation("Mock Document Search returned {Count} relevant documents.", filteredDocuments.Count);
            }

            return Task.FromResult(response);
        }
    }
}
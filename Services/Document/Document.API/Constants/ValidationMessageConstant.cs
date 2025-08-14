namespace Document.API.Constants;

public class ValidationMessageConstant
{
    public class Document
    {
        // Title validation
        public const string TitleRequired = "Tiêu đề không được để trống";
        public const string TitleMaxLength = "Tiêu đề không được vượt quá {0} ký tự";
        public const string TitleInvalidCharacters = "Tiêu đề chỉ được chứa chữ cái, số, khoảng trắng và các ký tự đặc biệt cơ bản";

        // Version name validation
        public const string VersionNameRequired = "Tên phiên bản không được để trống";
        public const string VersionNameMaxLength = "Tên phiên bản không được vượt quá {0} ký tự";
        public const string VersionNameInvalidCharacters = "Tên phiên bản chỉ được chứa chữ cái, số, khoảng trắng, dấu chấm và gạch ngang";

        // Summary validation
        public const string SummaryMaxLength = "Tóm tắt không được vượt quá {0} ký tự";
        public const string SummaryInvalidCharacters = "Tóm tắt chứa ký tự không hợp lệ";

        // Description validation
        public const string DescriptionMaxLength = "Mô tả không được vượt quá {0} ký tự";
        public const string DescriptionInvalidCharacters = "Mô tả chứa ký tự không hợp lệ";

        // SignedBy validation
        public const string SignedByMaxLength = "Người ký không được vượt quá {0} ký tự";
        public const string SignedByInvalidCharacters = "Tên người ký chỉ được chứa chữ cái, khoảng trắng và các ký tự tiếng Việt";

        // Approval Queue Filter validation
        public const string SubmittedByMaxLength = "ID người gửi không được vượt quá {0} ký tự";
        public const string SubmittedByInvalidCharacters = "ID người gửi chứa ký tự không hợp lệ";
        public const string ReviewedByMaxLength = "ID người phê duyệt không được vượt quá {0} ký tự";
        public const string ReviewedByInvalidCharacters = "ID người phê duyệt chứa ký tự không hợp lệ";
        public const string DocumentTypeIdMaxLength = "ID loại tài liệu không được vượt quá {0} ký tự";
        public const string DocumentTypeIdInvalidCharacters = "ID loại tài liệu chứa ký tự không hợp lệ";
        public const string FilterTitleMaxLength = "Tiêu đề lọc không được vượt quá {0} ký tự";
        public const string FilterTitleInvalidCharacters = "Tiêu đề lọc chứa ký tự không hợp lệ";
        public const string InvalidDateRange = "Ngày bắt đầu không được lớn hơn ngày kết thúc";
        public const string InvalidStatus = "Trạng thái không hợp lệ";
        public const string InvalidPageNumber = "Số trang phải lớn hơn 0";
        public const string InvalidPageSize = "Kích thước trang phải từ 1 đến 100";

        // File validation
        public const string FileRequired = "Tệp tin không được để trống";
        public const string FileTypeNotSupported = "Loại tệp tin không được hỗ trợ. Chỉ chấp nhận: {0}";
        public const string FileSizeExceeded = "Kích thước tệp tin không được vượt quá {0}MB";

        // DocumentType validation
        public const string DocumentTypeRequired = "Loại tài liệu không được để trống";
        public const string DocumentTypeInvalid = "Loại tài liệu không hợp lệ";

        // Date validation
        public const string EffectiveFromInvalid = "Ngày hiệu lực không hợp lệ";
        public const string EffectiveUntilInvalid = "Ngày hết hiệu lực không hợp lệ";
        public const string EffectiveDateRangeInvalid = "Ngày hiệu lực phải trước ngày hết hiệu lực";

        // Tags validation
        public const string TagsMaxCount = "Không được vượt quá {0} thẻ";
        public const string TagMaxLength = "Mỗi thẻ không được vượt quá {0} ký tự";
        public const string TagInvalidCharacters = "Thẻ chỉ được chứa chữ cái, số, khoảng trắng và gạch ngang";

        // ReplacementDocument validation
        public const string ReplacementDocumentInvalid = "Tài liệu thay thế không hợp lệ";
    }

    public class DocumentType
    {
        // Name validation
        public const string NameRequired = "Tên loại tài liệu không được để trống";
        public const string NameMaxLength = "Tên loại tài liệu không được vượt quá {0} ký tự";
        public const string NameMinLength = "Tên loại tài liệu phải có ít nhất {0} ký tự";
        public const string NameInvalidCharacters = "Tên loại tài liệu chỉ được chứa chữ cái, số, khoảng trắng, gạch ngang và gạch dưới";
        public const string NameAlreadyExists = "Tên loại tài liệu đã tồn tại";

        // Description validation
        public const string DescriptionMaxLength = "Mô tả không được vượt quá {0} ký tự";
        public const string DescriptionInvalidCharacters = "Mô tả chứa ký tự không hợp lệ";
    }

    public class DocumentRAG
    {
        // Query validation
        public const string QueryRequired = "Câu hỏi không được để trống";
        public const string QueryMaxLength = "Câu hỏi không được vượt quá {0} ký tự";
        public const string QueryMinLength = "Câu hỏi phải có ít nhất {0} ký tự";

        // UserId validation
        public const string UserIdRequired = "ID người dùng không được để trống";
        public const string UserIdInvalid = "ID người dùng không hợp lệ";

        // MaxResults validation
        public const string MaxResultsRange = "Số kết quả tối đa phải từ {0} đến {1}";

        // MinRelevanceScore validation
        public const string MinRelevanceScoreRange = "Điểm liên quan tối thiểu phải từ {0} đến {1}";

        // DepartmentId validation
        public const string DepartmentIdInvalid = "ID phòng ban không hợp lệ";

        // Tags validation
        public const string TagsMaxCount = "Không được vượt quá {0} thẻ";
        public const string TagMaxLength = "Mỗi thẻ không được vượt quá {0} ký tự";
    }

    public class DocumentRecommendation
    {
        // Count validation
        public const string CountRange = "Số lượng gợi ý phải từ {0} đến {1}";

        // DocumentTypeFilter validation
        public const string DocumentTypeFilterInvalid = "Bộ lọc loại tài liệu không hợp lệ";
    }

    public class DocumentReplacementSuggestion
    {
        // Title validation
        public const string TitleRequired = "Tiêu đề không được để trống";
        public const string TitleMaxLength = "Tiêu đề không được vượt quá {0} ký tự";

        // Description validation
        public const string DescriptionMaxLength = "Mô tả không được vượt quá {0} ký tự";

        // DocumentTypeId validation
        public const string DocumentTypeIdRequired = "ID loại tài liệu không được để trống";

        // MaxSuggestions validation
        public const string MaxSuggestionsRange = "Số gợi ý tối đa phải từ {0} đến {1}";

        // Tags validation
        public const string TagsMaxCount = "Không được vượt quá {0} thẻ";
        public const string TagMaxLength = "Mỗi thẻ không được vượt quá {0} ký tự";
    }

    public class SemanticSearch
    {
        // Query validation
        public const string QueryRequired = "Từ khóa tìm kiếm không được để trống";
        public const string QueryLengthRange = "Từ khóa tìm kiếm phải có từ {0} đến {1} ký tự";
        public const string QueryInvalidCharacters = "Từ khóa tìm kiếm chứa ký tự không hợp lệ";

        // MinRelevance validation
        public const string MinRelevanceRange = "Ngưỡng độ liên quan phải từ {0} đến {1}";

        // MaxResults validation
        public const string MaxResultsRange = "Số kết quả tối đa phải từ {0} đến {1}";

        // Scope validation
        public const string InvalidScope = "Phạm vi tìm kiếm không hợp lệ";
    }

    public class OfficialDocumentsFilter
    {
        // Title validation
        public const string TitleMaxLength = "Tiêu đề không được vượt quá {0} ký tự";
        public const string TitleInvalidCharacters = "Tiêu đề chứa ký tự không hợp lệ";

        // Keyword validation
        public const string KeywordMaxLength = "Từ khóa tìm kiếm không được vượt quá {0} ký tự";
        public const string KeywordInvalidCharacters = "Từ khóa tìm kiếm chứa ký tự không hợp lệ";

        // Version name validation
        public const string VersionNameMaxLength = "Tên phiên bản không được vượt quá {0} ký tự";
        public const string VersionNameInvalidCharacters = "Tên phiên bản chứa ký tự không hợp lệ";

        // Date validation
        public const string InvalidDateRange = "Ngày bắt đầu phải nhỏ hơn ngày kết thúc";
        public const string InvalidEffectiveDateRange = "Ngày hiệu lực bắt đầu phải nhỏ hơn ngày hiệu lực kết thúc";
        public const string InvalidSubmittedDateRange = "Ngày gửi bắt đầu phải nhỏ hơn ngày gửi kết thúc";

        // Document type validation
        public const string DocumentTypeIdInvalid = "ID loại tài liệu không hợp lệ";

        // Tags validation
        public const string TagsMaxCount = "Không được vượt quá {0} thẻ";
        public const string TagMaxLength = "Mỗi thẻ không được vượt quá {0} ký tự";
        public const string TagInvalidCharacters = "Thẻ chứa ký tự không hợp lệ";

        // SignedBy validation
        public const string SignedByMaxLength = "Người ký không được vượt quá {0} ký tự";
        public const string SignedByInvalidCharacters = "Người ký chứa ký tự không hợp lệ";

        // File type validation
        public const string FileTypeMaxLength = "Loại file không được vượt quá {0} ký tự";
        public const string FileTypeInvalid = "Loại file không hợp lệ";

        // File size validation
        public const string FileSizeRange = "Kích thước file phải từ {0} đến {1} bytes";
        public const string InvalidFileSizeRange = "Kích thước file tối thiểu phải nhỏ hơn kích thước file tối đa";

        // Download count validation
        public const string DownloadCountRange = "Số lượt tải phải từ {0} đến {1}";
        public const string InvalidDownloadCountRange = "Số lượt tải tối thiểu phải nhỏ hơn số lượt tải tối đa";

        // SubmittedBy validation
        public const string SubmittedByInvalid = "ID người gửi không hợp lệ";
    }
}

public static class ValidationConstants
{
    // Document validation constants
    public const int DocumentTitleMaxLength = 200;
    public const int DocumentVersionNameMaxLength = 100;
    public const int DocumentSummaryMaxLength = 10000;
    public const int DocumentDescriptionMaxLength = 2000;
    public const int DocumentSignedByMaxLength = 100;
    public const int DocumentTagMaxLength = 500;
    public const int DocumentTagsMaxCount = 10;

    // DocumentType validation constants
    public const int DocumentTypeNameMinLength = 2;
    public const int DocumentTypeNameMaxLength = 100;
    public const int DocumentTypeDescriptionMaxLength = 500;

    // DocumentRAG validation constants
    public const int DocumentRAGQueryMinLength = 3;
    public const int DocumentRAGQueryMaxLength = 5000;
    public const int DocumentRAGMaxResultsMin = 1;
    public const int DocumentRAGMaxResultsMax = 50;
    public const double DocumentRAGMinRelevanceScoreMin = 0.0;
    public const double DocumentRAGMinRelevanceScoreMax = 1.0;
    public const int DocumentRAGTagMaxLength = 5000;
    public const int DocumentRAGTagsMaxCount = 100;

    // DocumentRecommendation validation constants
    public const int DocumentRecommendationCountMin = 1;
    public const int DocumentRecommendationCountMax = 20;

    // SemanticSearch validation constants
    public const int SemanticSearchQueryMinLength = 3;
    public const int SemanticSearchQueryMaxLength = 500;
    public const double SemanticSearchMinRelevanceMin = 0.0;
    public const double SemanticSearchMinRelevanceMax = 1.0;
    public const int SemanticSearchMaxResultsMin = 1;
    public const int SemanticSearchMaxResultsMax = 100;

    // OfficialDocumentsFilter validation constants
    public const int OfficialDocumentsFilterTitleMaxLength = 200;
    public const int OfficialDocumentsFilterKeywordMaxLength = 500;
    public const int OfficialDocumentsFilterVersionNameMaxLength = 100;
    public const int OfficialDocumentsFilterDocumentTypeIdMaxLength = 50;
    public const int OfficialDocumentsFilterSignedByMaxLength = 200;
    public const int OfficialDocumentsFilterFileTypeMaxLength = 10;
    public const int OfficialDocumentsFilterSubmittedByMaxLength = 50;
    public const int OfficialDocumentsFilterTagMaxLength = 500;
    public const int OfficialDocumentsFilterTagsMaxCount = 20;
    public const long OfficialDocumentsFilterMaxFileSize = 1073741824; // 1GB
    public const int OfficialDocumentsFilterMaxDownloads = 999999;

    // Approval Queue Filter validation constants
    public const int ApprovalQueueFilterSubmittedByMaxLength = 50;
    public const int ApprovalQueueFilterReviewedByMaxLength = 50;
    public const int ApprovalQueueFilterDocumentTypeIdMaxLength = 50;
    public const int ApprovalQueueFilterTitleMaxLength = 200;
    public const int ApprovalQueuePageSizeMin = 1;
    public const int ApprovalQueuePageSizeMax = 100;

    // DocumentReplacementSuggestion validation constants
    public const int DocumentReplacementSuggestionTitleMaxLength = 200;
    public const int DocumentReplacementSuggestionDescriptionMaxLength = 1000;
    public const int DocumentReplacementSuggestionMaxSuggestionsMin = 1;
    public const int DocumentReplacementSuggestionMaxSuggestionsMax = 20;
    public const int DocumentReplacementSuggestionTagMaxLength = 50;
    public const int DocumentReplacementSuggestionTagsMaxCount = 10;

    // Common regex patterns
    public const string VietnameseTextRegex = @"^[a-zA-ZÀ-ỹ0-9\s\.\-_,;:!?\(\)\[\]""''/]+$";
    public const string DocumentTypeNameRegex = @"^[a-zA-ZÀ-ỹ0-9\s\-_]+$";
    public const string VersionNameRegex = @"^[a-zA-ZÀ-ỹ0-9\s\.\-_/]+$";
    public const string TagRegex = @"^[a-zA-ZÀ-ỹ0-9\s\-]+$";
    public const string VietnameseNameRegex = @"^[a-zA-ZÀ-ỹ\s]+$";
}

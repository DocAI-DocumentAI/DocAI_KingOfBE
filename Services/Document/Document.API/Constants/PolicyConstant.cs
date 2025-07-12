namespace Document.API.Constants;

public static class PolicyConstant
{
    public const int MaxDraftsPerUser = 20;
    public const int MaxFileSizeMB = 5;
    public static readonly string[] SupportedFileTypes = { ".pdf", ".docx" };
    public const int MaxSummaryLength = 1000;
}
namespace AI.API.Payload.Response
{
    public class PromptTemplateResponse : BaseResponse
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Template { get; set; }
        public string Category { get; set; }
        public bool IsActive { get; set; }
        public Dictionary<string, string> Variables { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
    public class PromptTemplateSummary
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public bool IsActive { get; set; }
        public int VariableCount { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}

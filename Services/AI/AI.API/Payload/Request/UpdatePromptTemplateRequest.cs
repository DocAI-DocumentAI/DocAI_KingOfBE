namespace AI.API.Payload.Request
{
    public class UpdatePromptTemplateRequest : CreatePromptTemplateRequest
    {
        public bool IsActive { get; set; } = true;
    }
}

using System.ComponentModel.DataAnnotations;

namespace AI.API.Payload.Request
{
    public class CreatePromptTemplateRequest
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        public string Template { get; set; }

        [StringLength(50)]
        public string Category { get; set; } = "General";

        public Dictionary<string, string> Variables { get; set; }
    }

}

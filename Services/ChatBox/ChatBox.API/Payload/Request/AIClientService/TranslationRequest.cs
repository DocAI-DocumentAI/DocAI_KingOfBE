using System.ComponentModel.DataAnnotations;

namespace ChatBox.API.Payload.Request.AIClientService
{
    public class TranslationRequest
    {
        [Required]
        public string Text { get; set; }

        [Required]
        public string TargetLanguage { get; set; }

        public string SourceLanguage { get; set; } = "auto";
        public bool PreserveFormatting { get; set; } = true;
    }
}

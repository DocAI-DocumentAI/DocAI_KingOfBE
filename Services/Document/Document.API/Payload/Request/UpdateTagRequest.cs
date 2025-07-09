using System.ComponentModel.DataAnnotations;

namespace Document.API.Payload.Request
{
    public class UpdateTagRequest
    {
        [Required]
        public string Name { get; set; }
    }
}

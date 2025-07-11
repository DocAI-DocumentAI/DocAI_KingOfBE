using System.ComponentModel.DataAnnotations;

namespace Document.API.Payload.Request
{
    public class CreateTagRequest
    {
        [Required]
        public string Name { get; set; }
    }
}

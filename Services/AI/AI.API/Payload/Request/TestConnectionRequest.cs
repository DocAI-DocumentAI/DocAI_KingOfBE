using System.ComponentModel.DataAnnotations;

namespace AI.API.Payload.Request
{
    public class TestConnectionRequest
    {
        [Required]
        public string ModelId { get; set; }

        [Required]
        public string ApiKey { get; set; }
    }
}

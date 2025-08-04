using System.ComponentModel.DataAnnotations;

namespace ChatBox.API.Payload.Request
{
    public class SetActiveModelRequest
    {

        [Required(ErrorMessage = "Tên model không được để trống")]
        [StringLength(100, ErrorMessage = "Tên model quá dài")]
        public string ModelName { get; set; }
    }
}

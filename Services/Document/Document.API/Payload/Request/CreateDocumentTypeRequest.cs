using System.ComponentModel.DataAnnotations;
using Document.API.Validation;

namespace Document.API.Payload.Request
{
    public class CreateDocumentTypeRequest
    {
        [Required(ErrorMessage = "Document type name is required")]
        [DocumentTypeNameValidation]
        public string Name { get; set; }

        [StringLength(500, ErrorMessage = "Description must not exceed 500 characters")]
        public string? Description { get; set; }
    }
}

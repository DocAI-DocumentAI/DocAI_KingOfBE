using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Document.API.Validation
{
    public class DocumentTypeNameValidationAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
        {
            if (value is not string name)
            {
                return false;
            }

            // Check if name is not empty or whitespace
            if (string.IsNullOrWhiteSpace(name))
            {
                ErrorMessage = "Document type name cannot be empty or whitespace.";
                return false;
            }

            // Check length
            if (name.Length > 100)
            {
                ErrorMessage = "Document type name must not exceed 100 characters.";
                return false;
            }

            // Check for valid characters (letters, numbers, spaces, hyphens, underscores)
            if (!Regex.IsMatch(name, @"^[\p{L}0-9\s\-_]+$"))
            {
                ErrorMessage = "Document type name can only contain letters, numbers, spaces, hyphens, and underscores.";
                return false;
            }

            // Check that it doesn't start or end with whitespace
            if (name != name.Trim())
            {
                ErrorMessage = "Document type name cannot start or end with whitespace.";
                return false;
            }

            // Check for consecutive spaces
            if (name.Contains("  "))
            {
                ErrorMessage = "Document type name cannot contain consecutive spaces.";
                return false;
            }

            return true;
        }
    }
}

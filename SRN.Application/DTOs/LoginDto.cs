using System.ComponentModel.DataAnnotations;

namespace SRN.Application.DTOs
{
    /// <summary>
    /// Data Transfer Object used to capture user login credentials.
    /// Includes built-in data annotations for immediate model state validation.
    /// </summary>
    public class LoginDto
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        public required string Password { get; set; }
    }
}
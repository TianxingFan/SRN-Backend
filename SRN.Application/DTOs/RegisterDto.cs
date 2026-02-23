using System.ComponentModel.DataAnnotations;

namespace SRN.Application.DTOs
{
    public class RegisterDto
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        [MinLength(6)]
        public required string Password { get; set; }

        public string? WalletAddress { get; set; }
    }
}
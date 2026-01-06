using System.ComponentModel.DataAnnotations;

namespace SRN.API.DTOs
{
    public class RegisterDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [MinLength(6)]
        public string Password { get; set; }

        public string? WalletAddress { get; set; }
    }
}
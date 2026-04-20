using System.ComponentModel.DataAnnotations;

namespace SRN.Application.DTOs
{
    /// <summary>
    /// Data Transfer Object used during the user registration process.
    /// Enforces basic security rules such as minimum password length at the API boundary.
    /// </summary>
    public class RegisterDto
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        [MinLength(6)]
        public required string Password { get; set; }

        // Optional Web3 wallet address for future blockchain interactions
        public string? WalletAddress { get; set; }
    }
}
using Microsoft.AspNetCore.Identity;

namespace SRN.Domain.Entities
{
    public class ApplicationUser : IdentityUser
    {
        public string? WalletAddress { get; set; }

        public string Role { get; set; } = "Researcher";
    }
}
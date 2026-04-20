using Microsoft.AspNetCore.Identity;

namespace SRN.Domain.Entities
{
    /// <summary>
    /// Represents a registered user within the SRN system.
    /// Extends the default ASP.NET Core IdentityUser to include Web3-specific 
    /// and domain-specific properties.
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
        // Optional property to store the user's decentralized Web3 wallet address (e.g., MetaMask)
        public string? WalletAddress { get; set; }

        // Application-level role designation (Defaults to 'Researcher')
        public string Role { get; set; } = "Researcher";

        // Entity Framework Core navigation property establishing a One-to-Many relationship
        // A single user can own multiple uploaded research artifacts.
        public virtual ICollection<Artifact> Artifacts { get; set; } = new List<Artifact>();
    }
}
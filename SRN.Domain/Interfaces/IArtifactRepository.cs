using SRN.Domain.Entities;

namespace SRN.Domain.Interfaces
{
    /// <summary>
    /// Repository Pattern Interface for Artifact data access.
    /// Abstracts the underlying database context (EF Core) away from the business logic,
    /// making the application highly testable and loosely coupled.
    /// </summary>
    public interface IArtifactRepository
    {
        Task<Artifact?> GetByIdAsync(Guid id);
        Task<Artifact?> GetByHashAsync(string fileHash);

        // Security-focused query to ensure a user can only access their own documents
        Task<Artifact?> GetByIdAndOwnerAsync(Guid id, string ownerId);

        Task<IEnumerable<Artifact>> GetHistoryByOwnerAsync(string ownerId);

        // Fetches only documents that have successfully completed the blockchain anchoring process
        Task<IEnumerable<Artifact>> GetPublicArtifactsAsync();

        // Standard CRUD operations
        Task AddAsync(Artifact artifact);
        Task UpdateAsync(Artifact artifact);
        Task DeleteAsync(Artifact artifact);

        Task<IEnumerable<Artifact>> GetAllAsync();
    }
}
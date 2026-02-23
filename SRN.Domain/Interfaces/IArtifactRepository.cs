using SRN.Domain.Entities;

namespace SRN.Domain.Interfaces
{
    public interface IArtifactRepository
    {
        Task<Artifact?> GetByIdAsync(Guid id);
        Task<Artifact?> GetByHashAsync(string fileHash);
        Task<Artifact?> GetByIdAndOwnerAsync(Guid id, string ownerId);
        Task<IEnumerable<Artifact>> GetHistoryByOwnerAsync(string ownerId);
        Task AddAsync(Artifact artifact);
        Task UpdateAsync(Artifact artifact);
    }
}
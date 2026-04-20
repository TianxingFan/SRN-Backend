using Microsoft.EntityFrameworkCore;
using SRN.Domain.Entities;
using SRN.Domain.Interfaces;
using SRN.Infrastructure.Persistence;

namespace SRN.Infrastructure.Repositories
{
    /// <summary>
    /// Concrete implementation of the IArtifactRepository interface using Entity Framework Core.
    /// Encapsulates all direct database queries and commands, preventing SQL or ORM logic 
    /// from leaking into the Application layer.
    /// </summary>
    public class ArtifactRepository : IArtifactRepository
    {
        private readonly ApplicationDbContext _context;

        public ArtifactRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Retrieves an artifact by its Primary Key.
        /// </summary>
        public async Task<Artifact?> GetByIdAsync(Guid id)
        {
            // FindAsync is optimized to check the local EF change tracker before hitting the database
            return await _context.Artifacts.FindAsync(id);
        }

        /// <summary>
        /// Retrieves an artifact by its unique cryptographic file hash.
        /// </summary>
        public async Task<Artifact?> GetByHashAsync(string fileHash)
        {
            return await _context.Artifacts.FirstOrDefaultAsync(a => a.FileHash == fileHash);
        }

        /// <summary>
        /// Security query: Ensures the requested artifact actually belongs to the specified user.
        /// </summary>
        public async Task<Artifact?> GetByIdAndOwnerAsync(Guid id, string ownerId)
        {
            return await _context.Artifacts.FirstOrDefaultAsync(a => a.ArtifactId == id && a.OwnerId == ownerId);
        }

        /// <summary>
        /// Fetches the upload history for a specific user, ordered chronologically (newest first).
        /// </summary>
        public async Task<IEnumerable<Artifact>> GetHistoryByOwnerAsync(string ownerId)
        {
            return await _context.Artifacts
                .Where(a => a.OwnerId == ownerId)
                .OrderByDescending(a => a.UploadDate)
                .ToListAsync();
        }

        /// <summary>
        /// Retrieves only the documents that have been successfully registered on the blockchain.
        /// </summary>
        public async Task<IEnumerable<Artifact>> GetPublicArtifactsAsync()
        {
            return await _context.Artifacts
                .Where(a => a.Status == "Registered")
                .OrderByDescending(a => a.UploadDate)
                .ToListAsync();
        }

        /// <summary>
        /// Retrieves all artifacts in the system regardless of status or owner (Admin functionality).
        /// </summary>
        public async Task<IEnumerable<Artifact>> GetAllAsync()
        {
            return await _context.Artifacts.OrderByDescending(a => a.UploadDate).ToListAsync();
        }

        /// <summary>
        /// Adds a new artifact to the EF Core tracking context and persists it to the database.
        /// </summary>
        public async Task AddAsync(Artifact artifact)
        {
            _context.Artifacts.Add(artifact);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Updates an existing artifact's metadata (e.g., status changes to "Registered").
        /// </summary>
        public async Task UpdateAsync(Artifact artifact)
        {
            _context.Artifacts.Update(artifact);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Deletes an artifact record from the database.
        /// </summary>
        public async Task DeleteAsync(Artifact artifact)
        {
            _context.Artifacts.Remove(artifact);
            await _context.SaveChangesAsync();
        }
    }
}
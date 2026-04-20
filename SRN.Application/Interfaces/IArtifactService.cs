using SRN.Application.DTOs;
using SRN.Domain.Entities;

namespace SRN.Application.Interfaces
{
    /// <summary>
    /// Defines the core business logic contract for managing research artifacts.
    /// By programming against this interface, the API controllers remain decoupled from the actual implementation.
    /// </summary>
    public interface IArtifactService
    {
        /// <summary>
        /// Processes a new document upload, computes its SHA-256 hash, and saves it locally.
        /// </summary>
        Task<(bool Success, string Message, Guid? ArtifactId, string? Hash)> UploadArtifactAsync(ArtifactUploadDto dto, string userId);

        /// <summary>
        /// Administrator action to approve a pending document and trigger background blockchain registration.
        /// </summary>
        Task<(bool Success, string Message)> ApproveAndRegisterArtifactAsync(Guid artifactId);

        /// <summary>
        /// Queries the Ethereum blockchain to verify if a document hash exists in the immutable registry.
        /// </summary>
        Task<(bool Registered, string Owner, DateTime? RegisteredAt)> VerifyArtifactAsync(string fileHash);

        /// <summary>
        /// Retrieves a specific document for download, ensuring the requester is the owner.
        /// </summary>
        Task<Artifact?> GetArtifactForDownloadAsync(Guid id, string userId);

        /// <summary>
        /// Retrieves a document for download with an override mechanism for system administrators.
        /// </summary>
        Task<Artifact?> GetArtifactForDownloadAsync(Guid id, string userId, bool isAdmin = false);

        /// <summary>
        /// Retrieves the submission history for a specific researcher.
        /// </summary>
        Task<IEnumerable<object>> GetHistoryAsync(string userId);

        /// <summary>
        /// Retrieves all successfully published and verified documents for public access.
        /// </summary>
        Task<IEnumerable<object>> GetPublicArtifactsAsync();

        /// <summary>
        /// Retrieves the complete list of documents across all states for the administrative dashboard.
        /// </summary>
        Task<IEnumerable<object>> GetAllArtifactsForAdminAsync();

        /// <summary>
        /// Retrieves a public document for download while actively recalculating its physical hash 
        /// to detect unauthorized tampering on the server.
        /// </summary>
        Task<(bool IsTampered, Artifact? Artifact)> GetPublicArtifactForDownloadAsync(Guid id);

        /// <summary>
        /// Permanently removes a document's physical file and database record.
        /// </summary>
        Task<bool> DeleteArtifactAsync(Guid id, string userId, bool isAdmin = false);
    }
}
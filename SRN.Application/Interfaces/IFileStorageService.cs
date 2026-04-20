using Microsoft.AspNetCore.Http;

namespace SRN.Application.Interfaces
{
    /// <summary>
    /// Contract for managing physical file operations.
    /// Abstracts the underlying storage mechanism (e.g., local disk, AWS S3, Azure Blob) from the business logic.
    /// </summary>
    public interface IFileStorageService
    {
        /// <summary>
        /// Persists an uploaded binary file to the designated storage location.
        /// </summary>
        Task<string> SaveFileAsync(IFormFile file, string subFolder = "artifacts");

        /// <summary>
        /// Dynamically recalculates the cryptographic hash of an existing physical file to ensure data integrity.
        /// </summary>
        Task<string?> CalculateExistingFileHashAsync(string filePath);
    }
}
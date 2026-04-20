using Microsoft.AspNetCore.Http;
using SRN.Application.Interfaces;
using System.Security.Cryptography;

namespace SRN.Infrastructure.Services
{
    /// <summary>
    /// Handles physical file operations on the host server's local file system.
    /// This abstracts the file I/O logic so the application could easily swap to cloud storage (e.g., Azure Blob) later.
    /// </summary>
    public class LocalFileStorageService : IFileStorageService
    {
        /// <summary>
        /// Securely saves an uploaded file to the server's disk using a unique identifier.
        /// </summary>
        public async Task<string> SaveFileAsync(IFormFile file, string subFolder = "artifacts")
        {
            // Resolve the absolute path to the designated upload directory
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", subFolder);

            // Ensure the target directory exists before attempting to write
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            // Prepend a GUID to the filename to prevent overwriting files with the exact same name
            var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            // Stream the incoming multipart form data directly to the disk to minimize memory footprint
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Return the relative path to be stored in the database
            return Path.Combine("Uploads", subFolder, uniqueFileName).Replace("\\", "/");
        }

        /// <summary>
        /// Calculates the SHA-256 hash of a file currently residing on the disk.
        /// Used primarily for the on-demand tamper detection security feature.
        /// </summary>
        public async Task<string?> CalculateExistingFileHashAsync(string filePath)
        {
            var absolutePath = Path.Combine(Directory.GetCurrentDirectory(), filePath);

            // If the physical file has been deleted or moved unexpectedly, return null to flag an anomaly
            if (!File.Exists(absolutePath)) return null;

            // Open a read-only stream to compute the hash without locking the file exclusively
            using (var sha256 = SHA256.Create())
            using (var stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var hashBytes = await sha256.ComputeHashAsync(stream);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
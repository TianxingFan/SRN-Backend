using Microsoft.AspNetCore.Http;
using SRN.Application.Interfaces;
using System.Security.Cryptography;

namespace SRN.Infrastructure.Services
{
    public class LocalFileStorageService : IFileStorageService
    {
        public async Task<string> SaveFileAsync(IFormFile file, string subFolder = "artifacts")
        {
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", subFolder);

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return Path.Combine("Uploads", subFolder, uniqueFileName).Replace("\\", "/");
        }

        public async Task<string?> CalculateExistingFileHashAsync(string filePath)
        {
            var absolutePath = Path.Combine(Directory.GetCurrentDirectory(), filePath);

            if (!File.Exists(absolutePath)) return null;

            using (var sha256 = SHA256.Create())
            using (var stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var hashBytes = await sha256.ComputeHashAsync(stream);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
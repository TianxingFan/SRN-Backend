using Microsoft.AspNetCore.Http;
using SRN.Application.Interfaces;

namespace SRN.Infrastructure.Services;

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
}
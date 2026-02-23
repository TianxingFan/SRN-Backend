using Microsoft.AspNetCore.Http;

namespace SRN.Application.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(IFormFile file, string subFolder = "artifacts");
}
using Microsoft.AspNetCore.Http;

namespace SRN.Application.DTOs
{
    /// <summary>
    /// Data Transfer Object (DTO) for handling document upload payloads from the client.
    /// Encapsulates both the file metadata and the physical binary stream.
    /// </summary>
    public class ArtifactUploadDto
    {
        public required string Title { get; set; }

        // IFormFile handles the incoming multipart/form-data payload efficiently
        public required IFormFile File { get; set; }
    }
}
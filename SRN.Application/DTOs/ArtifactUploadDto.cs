using Microsoft.AspNetCore.Http;

namespace SRN.Application.DTOs
{
    public class ArtifactUploadDto
    {
        public required string Title { get; set; }
        public required IFormFile File { get; set; }
    }
}
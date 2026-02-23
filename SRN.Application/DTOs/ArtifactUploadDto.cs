using Microsoft.AspNetCore.Http;

namespace SRN.Application.DTOs
{
    public class ArtifactUploadDto
    {
        public string Title { get; set; }
        public IFormFile File { get; set; }
    }
}
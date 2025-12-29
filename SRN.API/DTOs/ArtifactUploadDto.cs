using Microsoft.AspNetCore.Http;

namespace SRN.API.DTOs
{
    public class ArtifactUploadDto
    {
        public string Title { get; set; }
        public IFormFile File { get; set; }
    }
}
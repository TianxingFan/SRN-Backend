using Microsoft.AspNetCore.Mvc;
using SRN.Domain.Entities;
using SRN.Infrastructure.Persistence;
using SRN.Infrastructure.Blockchain;
using System.Security.Cryptography;
using SRN.API.DTOs;

namespace SRN.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ArtifactsController : ControllerBase
    {
        private readonly SrnDbContext _context;
        private readonly BlockchainService _blockchainService;

        public ArtifactsController(SrnDbContext context, BlockchainService blockchainService)
        {
            _context = context;
            _blockchainService = blockchainService;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] ArtifactUploadDto uploadDto)
        {
            // Validate that a file was actually provided
            if (uploadDto.File == null || uploadDto.File.Length == 0)
                return BadRequest("No file uploaded.");

            string fileHash;

            // Step 1: Compute SHA256 hash (Digital Fingerprint)
            using (var sha256 = SHA256.Create())
            {
                using (var stream = uploadDto.File.OpenReadStream())
                {
                    var hashBytes = await sha256.ComputeHashAsync(stream);
                    fileHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                }
            }

            // Step 2: Check for duplicates in the database
            var existingArtifact = _context.Artifacts.FirstOrDefault(a => a.FileHash == fileHash);
            if (existingArtifact != null)
            {
                return Conflict(new { message = "Artifact already exists", artifactId = existingArtifact.ArtifactId });
            }

            // Step 3: Save the file to local storage
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var filePath = Path.Combine(uploadsFolder, $"{Guid.NewGuid()}_{uploadDto.File.FileName}");

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await uploadDto.File.CopyToAsync(stream);
            }

            // Step 4: Save initial metadata to the database
            var artifact = new Artifact
            {
                ArtifactId = Guid.NewGuid(),
                Title = uploadDto.Title,
                FileHash = fileHash,
                FilePath = filePath,
                UploadDate = DateTime.UtcNow,
                Status = "Pending Blockchain",
                OwnerId = Guid.NewGuid() // Placeholder: Replace with actual User ID in production
            };

            _context.Artifacts.Add(artifact);
            await _context.SaveChangesAsync();

            // Step 5: Anchor the hash to the Blockchain
            string txHash = "";
            try
            {
                // Trigger the blockchain transaction
                txHash = await _blockchainService.RegisterArtifactAsync(fileHash);

                // Update database status on success
                artifact.Status = "Verified On-Chain";
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Handle blockchain failure (e.g., insufficient gas, network error)
                return StatusCode(500, new { message = "Database saved, but Blockchain failed", error = ex.Message });
            }

            return Ok(new
            {
                message = "Upload & Anchoring Successful",
                artifactId = artifact.ArtifactId,
                hash = fileHash,
                blockchainTx = txHash
            });
        }
    }
}
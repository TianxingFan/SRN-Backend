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
        private readonly ApplicationDbContext _context;
        private readonly BlockchainService _blockchainService;

        public ArtifactsController(ApplicationDbContext context, BlockchainService blockchainService)
        {
            _context = context;
            _blockchainService = blockchainService;
        }

        // POST: api/Artifacts/upload
        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] ArtifactUploadDto uploadDto)
        {
            // 1. Validation: Ensure a file was actually uploaded
            if (uploadDto.File == null || uploadDto.File.Length == 0)
                return BadRequest("No file uploaded.");

            string fileHash;

            // 2. Hashing: Compute the SHA256 digital fingerprint of the file
            using (var sha256 = SHA256.Create())
            {
                using (var stream = uploadDto.File.OpenReadStream())
                {
                    var hashBytes = await sha256.ComputeHashAsync(stream);
                    fileHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                }
            }

            // 3. Deduplication: Check if this file already exists in our database
            var existingArtifact = _context.Artifacts.FirstOrDefault(a => a.FileHash == fileHash);
            if (existingArtifact != null)
            {
                return Conflict(new { message = "Artifact already exists", artifactId = existingArtifact.ArtifactId });
            }

            // 4. Storage: Save the physical file to the local "Uploads" directory
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            // Use a GUID prefix to prevent filename collisions
            var filePath = Path.Combine(uploadsFolder, $"{Guid.NewGuid()}_{uploadDto.File.FileName}");

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await uploadDto.File.CopyToAsync(stream);
            }

            // 5. Database: Create the initial record in PostgreSQL
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

            // 6. Blockchain: Anchor the hash to the Ethereum network
            string txHash = "";
            try
            {
                // Call the service to interact with the Smart Contract
                txHash = await _blockchainService.RegisterArtifactAsync(fileHash);

                // Update the status in the database upon success
                artifact.Status = "Verified On-Chain";
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // If blockchain fails (e.g., out of gas), return 500 but keep the DB record
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

        // GET: api/Artifacts/verify/{fileHash}
        // Queries the Blockchain directly to verify authenticity, bypassing the local DB
        [HttpGet("verify/{fileHash}")]
        public async Task<IActionResult> Verify(string fileHash)
        {
            // 1. Input Validation
            if (string.IsNullOrEmpty(fileHash))
            {
                return BadRequest("Hash is required.");
            }

            try
            {
                // 2. Call Blockchain Service (Read-only call to Smart Contract)
                var result = await _blockchainService.VerifyArtifactAsync(fileHash);

                // 3. Process Result
                if (result.Registered)
                {
                    // Convert Unix timestamp to local time for display
                    var verifyTime = DateTimeOffset.FromUnixTimeSeconds(result.Timestamp).DateTime.ToLocalTime();

                    return Ok(new
                    {
                        status = "Verified ✅",
                        message = "The document is authentically anchored on the Blockchain.",
                        details = new
                        {
                            ownerAddress = result.Owner,
                            registeredAt = verifyTime,
                            hash = fileHash
                        }
                    });
                }
                else
                {
                    return NotFound(new
                    {
                        status = "Unverified ❌",
                        message = "This hash does not exist on the Smart Contract."
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // GET: api/Artifacts/download/{id}
        // Retrieves the physical file for a given database ID
        [HttpGet("download/{id}")]
        public async Task<IActionResult> Download(Guid id)
        {
            // 1. Retrieve metadata from Database
            var artifact = await _context.Artifacts.FindAsync(id);
            if (artifact == null) return NotFound("File record not found.");

            // 2. Verify physical file existence
            if (!System.IO.File.Exists(artifact.FilePath))
                return NotFound("Physical file is missing on server.");

            // 3. Stream file into memory
            // Copy to MemoryStream to ensure the file handle is released quickly
            var memory = new MemoryStream();
            using (var stream = new FileStream(artifact.FilePath, FileMode.Open))
            {
                await stream.CopyToAsync(memory);
            }
            memory.Position = 0;

            // 4. Return file stream (MIME type defaults to binary stream)
            return File(memory, "application/octet-stream", Path.GetFileName(artifact.FilePath));
        }
    }
}
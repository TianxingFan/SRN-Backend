using Microsoft.AspNetCore.Mvc;
using Moq;
using SRN.API.Controllers;
using SRN.Application.Interfaces;

namespace SRN.API.Tests.Controllers
{
    /// <summary>
    /// Unit tests for the ArtifactsController. 
    /// Isolates the controller logic by mocking the underlying application service layer.
    /// </summary>
    public class ArtifactsControllerTests
    {
        // Mock object to simulate the behavior of the artifact service without hitting the DB or Blockchain
        private readonly Mock<IArtifactService> _mockArtifactService;

        // The System Under Test (SUT)
        private readonly ArtifactsController _controller;

        public ArtifactsControllerTests()
        {
            // Initialize the mock service
            _mockArtifactService = new Mock<IArtifactService>();

            // Inject the mocked service into the controller instance
            _controller = new ArtifactsController(_mockArtifactService.Object);
        }

        /// <summary>
        /// Tests that verifying a valid, registered cryptographic hash returns an HTTP 200 OK response.
        /// </summary>
        [Fact]
        public async Task Verify_WithValidHash_ReturnsOkResult()
        {
            // Arrange: Set up the mock data and expected behavior
            var fakeHash = "abc123hash";
            var fakeDate = DateTime.UtcNow;

            _mockArtifactService
                .Setup(s => s.VerifyArtifactAsync(fakeHash))
                .ReturnsAsync((Registered: true, Owner: "0xFakeWallet", RegisteredAt: fakeDate));

            // Act: Execute the controller action
            var result = await _controller.Verify(fakeHash);

            // Assert: Verify the result type and the response payload content
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("Verified", okResult.Value?.ToString() ?? "");
        }

        /// <summary>
        /// Tests that verifying an unregistered or invalid hash gracefully returns an HTTP 404 Not Found response.
        /// </summary>
        [Fact]
        public async Task Verify_WithUnregisteredHash_ReturnsNotFound()
        {
            // Arrange: Set up a hash that simulates a failure condition
            var fakeHash = "unknownHash";

            _mockArtifactService
                .Setup(s => s.VerifyArtifactAsync(fakeHash))
                .ReturnsAsync((Registered: false, Owner: string.Empty, RegisteredAt: null));

            // Act: Execute the controller action
            var result = await _controller.Verify(fakeHash);

            // Assert: Verify the controller returns a 404 response with the appropriate error text
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Contains("Unverified", notFoundResult.Value?.ToString() ?? "");
        }
    }
}
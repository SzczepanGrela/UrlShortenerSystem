using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using UrlShortener.API.Controllers;
using UrlShortener.API.Services;
using UrlShortener.CrossCutting.Dtos;
using UrlShortenerSystem.Common.CrossCutting.Dtos;

namespace UrlShortener.Tests
{
    public class LinksControllerTests : IDisposable
    {
        private readonly Mock<ILinkService> _mockLinkService;
        private readonly Mock<ILogger<LinksController>> _mockLogger;
        private readonly LinksController _controller;

        public LinksControllerTests()
        {
            _mockLinkService = new Mock<ILinkService>();
            _mockLogger = new Mock<ILogger<LinksController>>();
            _controller = new LinksController(_mockLinkService.Object);
        }

        [Fact]
        public async Task CreateLink_ValidRequest_ShouldReturnCreatedResult()
        {
            // Arrange
            var request = new CreateLinkRequestDto
            {
                OriginalUrl = "https://example.com"
            };

            var expectedLink = new LinkDto
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://example.com",
                ShortCode = "ABC123",
                ShortUrl = "https://localhost:7000/ABC123",
                CreationDate = DateTime.UtcNow
            };

            var successResult = new CrudOperationResult<LinkDto>
            {
                Status = CrudOperationResultStatus.Success,
                Result = expectedLink
            };

            _mockLinkService.Setup(s => s.CreateLink(It.IsAny<CreateLinkRequestDto>()))
                .ReturnsAsync(successResult);

            // Act
            var result = await _controller.CreateLink(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(nameof(LinksController.GetLinkByShortCode), createdResult.ActionName);
            Assert.Equal(expectedLink.ShortCode, createdResult.RouteValues["shortCode"]);
            Assert.Equal(expectedLink, createdResult.Value);
        }

        [Fact]
        public async Task CreateLink_ServiceError_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new CreateLinkRequestDto
            {
                OriginalUrl = "https://example.com"
            };

            var errorResult = new CrudOperationResult<LinkDto>
            {
                Status = CrudOperationResultStatus.Error,
                ErrorMessage = "Invalid URL"
            };

            _mockLinkService.Setup(s => s.CreateLink(It.IsAny<CreateLinkRequestDto>()))
                .ReturnsAsync(errorResult);

            // Act
            var result = await _controller.CreateLink(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Invalid URL", badRequestResult.Value);
        }

        [Fact]
        public async Task CreateLink_InvalidModel_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new CreateLinkRequestDto
            {
                OriginalUrl = "" // Invalid empty URL
            };

            _controller.ModelState.AddModelError("OriginalUrl", "URL is required");

            // Act
            var result = await _controller.CreateLink(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.IsType<SerializableError>(badRequestResult.Value);
        }

        [Fact]
        public async Task GetLinkByShortCode_ExistingLink_ShouldReturnOk()
        {
            // Arrange
            var shortCode = "ABC123";
            var expectedLink = new LinkDto
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://example.com",
                ShortCode = shortCode,
                ShortUrl = "https://localhost:7000/ABC123",
                CreationDate = DateTime.UtcNow
            };

            _mockLinkService.Setup(s => s.GetLinkByShortCode(shortCode))
                .ReturnsAsync(expectedLink);

            // Act
            var result = await _controller.GetLinkByShortCode(shortCode);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(expectedLink, okResult.Value);
        }

        [Fact]
        public async Task GetLinkByShortCode_NonExistingLink_ShouldReturnNotFound()
        {
            // Arrange
            var shortCode = "NONEXISTENT";

            _mockLinkService.Setup(s => s.GetLinkByShortCode(shortCode))
                .ReturnsAsync((LinkDto)null);

            // Act
            var result = await _controller.GetLinkByShortCode(shortCode);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteLink_ExistingLink_ShouldReturnOk()
        {
            // Arrange
            var linkId = Guid.NewGuid();
            var successResult = new CrudOperationResult<bool>
            {
                Status = CrudOperationResultStatus.Success,
                Result = true
            };

            _mockLinkService.Setup(s => s.DeleteLink(linkId))
                .ReturnsAsync(successResult);

            // Act
            var result = await _controller.DeleteLink(linkId);

            // Assert
            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public async Task DeleteLink_NonExistingLink_ShouldReturnNotFound()
        {
            // Arrange
            var linkId = Guid.NewGuid();
            var notFoundResult = new CrudOperationResult<bool>
            {
                Status = CrudOperationResultStatus.NotFound,
                ErrorMessage = "Link not found"
            };

            _mockLinkService.Setup(s => s.DeleteLink(linkId))
                .ReturnsAsync(notFoundResult);

            // Act
            var result = await _controller.DeleteLink(linkId);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteLink_ServiceError_ShouldReturnBadRequest()
        {
            // Arrange
            var linkId = Guid.NewGuid();
            var errorResult = new CrudOperationResult<bool>
            {
                Status = CrudOperationResultStatus.Error,
                ErrorMessage = "Database error"
            };

            _mockLinkService.Setup(s => s.DeleteLink(linkId))
                .ReturnsAsync(errorResult);

            // Act
            var result = await _controller.DeleteLink(linkId);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Database error", badRequestResult.Value);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task GetLinkByShortCode_InvalidShortCode_ShouldReturnBadRequest(string shortCode)
        {
            // Arrange
            // No need to setup mock service as validation happens before service call

            // Act
            var result = await _controller.GetLinkByShortCode(shortCode);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task CreateLink_WithExpirationDate_ShouldPassToService()
        {
            // Arrange
            var expirationDate = DateTime.UtcNow.AddDays(30);
            var request = new CreateLinkRequestDto
            {
                OriginalUrl = "https://example.com",
                ExpirationDate = expirationDate
            };

            var expectedLink = new LinkDto
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://example.com",
                ShortCode = "ABC123",
                ShortUrl = "https://localhost:7000/ABC123",
                CreationDate = DateTime.UtcNow,
                ExpirationDate = expirationDate
            };

            var successResult = new CrudOperationResult<LinkDto>
            {
                Status = CrudOperationResultStatus.Success,
                Result = expectedLink
            };

            _mockLinkService.Setup(s => s.CreateLink(It.Is<CreateLinkRequestDto>(r => 
                r.OriginalUrl == "https://example.com" && r.ExpirationDate == expirationDate)))
                .ReturnsAsync(successResult);

            // Act
            var result = await _controller.CreateLink(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            var returnedLink = Assert.IsType<LinkDto>(createdResult.Value);
            Assert.Equal(expirationDate, returnedLink.ExpirationDate);
        }

        public void Dispose()
        {
            // Clean up resources if needed
        }
    }
}
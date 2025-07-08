using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Analytics.API.Controllers;
using Analytics.API.Services;
using Analytics.CrossCutting.Dtos;
using UrlShortenerSystem.Common.CrossCutting.Dtos;

namespace Analytics.Tests
{
    public class AnalyticsControllerTests
    {
        private readonly Mock<IAnalyticsService> _mockAnalyticsService;
        private readonly AnalyticsController _controller;

        public AnalyticsControllerTests()
        {
            _mockAnalyticsService = new Mock<IAnalyticsService>();
            _controller = new AnalyticsController(_mockAnalyticsService.Object);
        }

        [Fact]
        public async Task RegisterClick_ValidRequest_ShouldReturnOk()
        {
            // Arrange
            var request = new RegisterClickRequestDto
            {
                LinkId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                IpAddress = "192.168.1.1",
                UserAgent = "Mozilla/5.0",
                Referer = "https://google.com"
            };

            var expectedClick = new ClickDto
            {
                Id = Guid.NewGuid(),
                LinkId = request.LinkId,
                Timestamp = request.Timestamp,
                IpAddress = request.IpAddress,
                UserAgent = request.UserAgent,
                Referer = request.Referer
            };

            var successResult = new CrudOperationResult<ClickDto>
            {
                Status = CrudOperationResultStatus.Success,
                Result = expectedClick
            };

            _mockAnalyticsService.Setup(s => s.RegisterClick(It.IsAny<RegisterClickRequestDto>()))
                .ReturnsAsync(successResult);

            // Act
            var result = await _controller.RegisterClick(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(expectedClick, okResult.Value);
        }

        [Fact]
        public async Task RegisterClick_ServiceError_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new RegisterClickRequestDto
            {
                LinkId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                IpAddress = "192.168.1.1"
            };

            var errorResult = new CrudOperationResult<ClickDto>
            {
                Status = CrudOperationResultStatus.Error,
                ErrorMessage = "Database connection failed"
            };

            _mockAnalyticsService.Setup(s => s.RegisterClick(It.IsAny<RegisterClickRequestDto>()))
                .ReturnsAsync(errorResult);

            // Act
            var result = await _controller.RegisterClick(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Database connection failed", badRequestResult.Value);
        }

        [Fact]
        public async Task RegisterClick_InvalidModel_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new RegisterClickRequestDto
            {
                LinkId = Guid.Empty, // Invalid empty GUID
                Timestamp = DateTime.UtcNow
            };

            _controller.ModelState.AddModelError("LinkId", "LinkId is required");

            // Act
            var result = await _controller.RegisterClick(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.IsType<SerializableError>(badRequestResult.Value);
        }

        [Fact]
        public async Task GetStatsForLink_ShouldReturnStats()
        {
            // Arrange
            var linkId = Guid.NewGuid();
            var expectedStats = new LinkStatsDto
            {
                LinkId = linkId,
                TotalClicks = 100,
                UniqueClicks = 75,
                FirstClick = DateTime.UtcNow.AddDays(-10),
                LastClick = DateTime.UtcNow,
                DailyStats = new List<DailyClickStatsDto>
                {
                    new DailyClickStatsDto { Date = DateTime.UtcNow.Date, Clicks = 10 }
                },
                CountryStats = new List<CountryStatsDto>
                {
                    new CountryStatsDto { Country = "Poland", Clicks = 50 }
                }
            };

            _mockAnalyticsService.Setup(s => s.GetStatsForLink(linkId))
                .ReturnsAsync(expectedStats);

            // Act
            var result = await _controller.GetStatsForLink(linkId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(expectedStats, okResult.Value);
        }

        [Fact]
        public async Task GetClicksForLink_DefaultPagination_ShouldReturnClicks()
        {
            // Arrange
            var linkId = Guid.NewGuid();
            var expectedClicks = new List<ClickDto>
            {
                new ClickDto
                {
                    Id = Guid.NewGuid(),
                    LinkId = linkId,
                    Timestamp = DateTime.UtcNow,
                    IpAddress = "192.168.1.1"
                }
            };

            _mockAnalyticsService.Setup(s => s.GetClicksForLink(linkId, 1, 50))
                .ReturnsAsync(expectedClicks);

            // Act
            var result = await _controller.GetClicksForLink(linkId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(expectedClicks, okResult.Value);
        }

        [Fact]
        public async Task GetClicksForLink_CustomPagination_ShouldPassParametersToService()
        {
            // Arrange
            var linkId = Guid.NewGuid();
            var page = 2;
            var pageSize = 25;
            var expectedClicks = new List<ClickDto>();

            _mockAnalyticsService.Setup(s => s.GetClicksForLink(linkId, page, pageSize))
                .ReturnsAsync(expectedClicks);

            // Act
            var result = await _controller.GetClicksForLink(linkId, page, pageSize);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(expectedClicks, okResult.Value);
            _mockAnalyticsService.Verify(s => s.GetClicksForLink(linkId, page, pageSize), Times.Once);
        }

        [Fact]
        public async Task GetClicksForLink_PageSizeExceedsMaximum_ShouldReturnBadRequest()
        {
            // Arrange
            var linkId = Guid.NewGuid();
            var pageSize = 1001; // Exceeds maximum of 1000

            // Act
            var result = await _controller.GetClicksForLink(linkId, 1, pageSize);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Page size must be between 1 and 1000", badRequestResult.Value.ToString());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-10)]
        public async Task GetClicksForLink_InvalidPageSize_ShouldReturnBadRequest(int pageSize)
        {
            // Arrange
            var linkId = Guid.NewGuid();

            // Act
            var result = await _controller.GetClicksForLink(linkId, 1, pageSize);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Page size must be between 1 and 1000", badRequestResult.Value.ToString());
        }

        [Fact]
        public async Task GetStatsForLink_EmptyGuid_ShouldReturnBadRequest()
        {
            // Arrange
            var linkId = Guid.Empty;

            // Act
            var result = await _controller.GetStatsForLink(linkId);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Valid link ID is required", badRequestResult.Value);
        }

        [Fact]
        public async Task RegisterClick_WithGeoLocationData_ShouldPassToService()
        {
            // Arrange
            var request = new RegisterClickRequestDto
            {
                LinkId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                IpAddress = "192.168.1.1",
                UserAgent = "Mozilla/5.0",
                Referer = "https://google.com"
            };

            var expectedClick = new ClickDto
            {
                Id = Guid.NewGuid(),
                LinkId = request.LinkId,
                Timestamp = request.Timestamp,
                IpAddress = request.IpAddress,
                UserAgent = request.UserAgent,
                Referer = request.Referer,
                Country = "Poland",
                City = "Warsaw"
            };

            var successResult = new CrudOperationResult<ClickDto>
            {
                Status = CrudOperationResultStatus.Success,
                Result = expectedClick
            };

            _mockAnalyticsService.Setup(s => s.RegisterClick(It.Is<RegisterClickRequestDto>(r => 
                r.LinkId == request.LinkId && r.IpAddress == request.IpAddress)))
                .ReturnsAsync(successResult);

            // Act
            var result = await _controller.RegisterClick(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedClick = Assert.IsType<ClickDto>(okResult.Value);
            Assert.Equal("Poland", returnedClick.Country);
            Assert.Equal("Warsaw", returnedClick.City);
        }

        [Fact]
        public async Task RegisterClick_MinimalData_ShouldSucceed()
        {
            // Arrange
            var request = new RegisterClickRequestDto
            {
                LinkId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow
                // No optional fields
            };

            var expectedClick = new ClickDto
            {
                Id = Guid.NewGuid(),
                LinkId = request.LinkId,
                Timestamp = request.Timestamp
            };

            var successResult = new CrudOperationResult<ClickDto>
            {
                Status = CrudOperationResultStatus.Success,
                Result = expectedClick
            };

            _mockAnalyticsService.Setup(s => s.RegisterClick(It.IsAny<RegisterClickRequestDto>()))
                .ReturnsAsync(successResult);

            // Act
            var result = await _controller.RegisterClick(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(expectedClick, okResult.Value);
        }
    }
}
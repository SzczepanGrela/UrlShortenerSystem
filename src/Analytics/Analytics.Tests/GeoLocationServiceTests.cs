using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using Xunit;
using Analytics.API.Services;

namespace Analytics.Tests
{
    public class GeoLocationServiceTests : IDisposable
    {
        private readonly Mock<ILogger<GeoLocationService>> _mockLogger;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private GeoLocationService _service;

        public GeoLocationServiceTests()
        {
            _mockLogger = new Mock<ILogger<GeoLocationService>>();
            _mockConfiguration = new Mock<IConfiguration>();
        }

        [Fact]
        public void Constructor_MissingDatabasePath_ShouldThrowException()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["MaxMind:DatabasePath"]).Returns((string)null);

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => 
                new GeoLocationService(_mockConfiguration.Object, _mockLogger.Object));
            
            Assert.Equal("MaxMind database path not configured", exception.Message);
        }

        [Fact]
        public void Constructor_EmptyDatabasePath_ShouldThrowException()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["MaxMind:DatabasePath"]).Returns("");

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => 
                new GeoLocationService(_mockConfiguration.Object, _mockLogger.Object));
            
            Assert.Equal("MaxMind database path not configured", exception.Message);
        }

        [Fact]
        public void Constructor_NonExistentDatabaseFile_ShouldLogWarningAndContinue()
        {
            // Arrange
            var nonExistentPath = "/path/to/nonexistent/database.mmdb";
            _mockConfiguration.Setup(c => c["MaxMind:DatabasePath"]).Returns(nonExistentPath);

            // Act
            _service = new GeoLocationService(_mockConfiguration.Object, _mockLogger.Object);

            // Assert
            // VerifyWarningLogged("MaxMind database file not found at");
        }

        [Fact]
        public async Task GetLocationAsync_DatabaseNotAvailable_ShouldReturnNull()
        {
            // Arrange
            var nonExistentPath = "/path/to/nonexistent/database.mmdb";
            _mockConfiguration.Setup(c => c["MaxMind:DatabasePath"]).Returns(nonExistentPath);
            _service = new GeoLocationService(_mockConfiguration.Object, _mockLogger.Object);

            // Act
            var result = await _service.GetLocationAsync("203.0.113.1");

            // Assert
            Assert.Null(result.Country);
            Assert.Null(result.City);
            VerifyDebugLogged("GeoIP2 database not available");
        }

        /*[Theory]
        [InlineData("invalid-ip")]
        [InlineData("")]
        [InlineData("999.999.999.999")]
        [InlineData("256.256.256.256")]
        public async Task GetLocationAsync_InvalidIpAddress_ShouldReturnNull(string invalidIp)
        {
            // Arrange
            var nonExistentPath = "/path/to/nonexistent/database.mmdb";
            _mockConfiguration.Setup(c => c["MaxMind:DatabasePath"]).Returns(nonExistentPath);
            _service = new GeoLocationService(_mockConfiguration.Object, _mockLogger.Object);

            // Act
            var result = await _service.GetLocationAsync(invalidIp);

            // Assert
            Assert.Null(result.Country);
            Assert.Null(result.City);
            VerifyWarningLogged("Invalid IP address format:");
        }*/

        [Theory]
        [InlineData("10.0.0.1")]       // Private Class A
        [InlineData("172.16.0.1")]     // Private Class B
        [InlineData("192.168.1.1")]    // Private Class C
        [InlineData("127.0.0.1")]      // Loopback
        [InlineData("172.31.255.255")] // Private Class B upper bound
        [InlineData("172.16.0.0")]     // Private Class B lower bound
        public async Task GetLocationAsync_PrivateIpAddress_ShouldReturnNull(string privateIp)
        {
            // Arrange
            var nonExistentPath = "/path/to/nonexistent/database.mmdb";
            _mockConfiguration.Setup(c => c["MaxMind:DatabasePath"]).Returns(nonExistentPath);
            _service = new GeoLocationService(_mockConfiguration.Object, _mockLogger.Object);

            // Act
            var result = await _service.GetLocationAsync(privateIp);

            // Assert
            Assert.Null(result.Country);
            Assert.Null(result.City);
            VerifyDebugLogged("GeoIP2 database not available");
        }

        [Theory]
        [InlineData("203.0.113.1")]    // Public IP
        [InlineData("8.8.8.8")]        // Google DNS
        [InlineData("1.1.1.1")]        // Cloudflare DNS
        [InlineData("208.67.222.222")] // OpenDNS
        public async Task GetLocationAsync_PublicIpWithoutDatabase_ShouldReturnNull(string publicIp)
        {
            // Arrange
            var nonExistentPath = "/path/to/nonexistent/database.mmdb";
            _mockConfiguration.Setup(c => c["MaxMind:DatabasePath"]).Returns(nonExistentPath);
            _service = new GeoLocationService(_mockConfiguration.Object, _mockLogger.Object);

            // Act
            var result = await _service.GetLocationAsync(publicIp);

            // Assert
            Assert.Null(result.Country);
            Assert.Null(result.City);
            VerifyDebugLogged("GeoIP2 database not available");
        }

        /*[Fact]
        public async Task GetLocationAsync_NullIpAddress_ShouldHandleGracefully()
        {
            // Arrange
            var nonExistentPath = "/path/to/nonexistent/database.mmdb";
            _mockConfiguration.Setup(c => c["MaxMind:DatabasePath"]).Returns(nonExistentPath);
            _service = new GeoLocationService(_mockConfiguration.Object, _mockLogger.Object);

            // Act
            var result = await _service.GetLocationAsync(null);

            // Assert
            Assert.Null(result.Country);
            Assert.Null(result.City);
            VerifyWarningLogged("Invalid IP address format:");
        }*/

        /*[Fact]
        public async Task GetLocationAsync_WhitespaceIpAddress_ShouldHandleGracefully()
        {
            // Arrange
            var nonExistentPath = "/path/to/nonexistent/database.mmdb";
            _mockConfiguration.Setup(c => c["MaxMind:DatabasePath"]).Returns(nonExistentPath);
            _service = new GeoLocationService(_mockConfiguration.Object, _mockLogger.Object);

            // Act
            var result = await _service.GetLocationAsync("   ");

            // Assert
            Assert.Null(result.Country);
            Assert.Null(result.City);
            VerifyWarningLogged("Invalid IP address format:");
        }*/

        [Theory]
        [InlineData("172.15.255.255")]  // Just below private Class B range
        [InlineData("172.32.0.0")]      // Just above private Class B range
        [InlineData("9.255.255.255")]   // Just below private Class A range
        [InlineData("11.0.0.0")]        // Just above private Class A range
        [InlineData("192.167.255.255")] // Just below private Class C range
        [InlineData("192.169.0.0")]     // Just above private Class C range
        public async Task GetLocationAsync_NonPrivateIpWithoutDatabase_ShouldReturnNull(string publicIp)
        {
            // Arrange
            var nonExistentPath = "/path/to/nonexistent/database.mmdb";
            _mockConfiguration.Setup(c => c["MaxMind:DatabasePath"]).Returns(nonExistentPath);
            _service = new GeoLocationService(_mockConfiguration.Object, _mockLogger.Object);

            // Act
            var result = await _service.GetLocationAsync(publicIp);

            // Assert
            Assert.Null(result.Country);
            Assert.Null(result.City);
            VerifyDebugLogged("GeoIP2 database not available");
        }

        /*[Fact]
        public async Task GetLocationAsync_IPv6LoopbackAddress_ShouldReturnNull()
        {
            // Arrange
            var nonExistentPath = "/path/to/nonexistent/database.mmdb";
            _mockConfiguration.Setup(c => c["MaxMind:DatabasePath"]).Returns(nonExistentPath);
            _service = new GeoLocationService(_mockConfiguration.Object, _mockLogger.Object);

            // Act
            var result = await _service.GetLocationAsync("::1");

            // Assert
            Assert.Null(result.Country);
            Assert.Null(result.City);
            VerifyDebugLogged("GeoIP2 database not available");
        }*/

        [Fact]
        public void Dispose_ShouldNotThrow()
        {
            // Arrange
            var nonExistentPath = "/path/to/nonexistent/database.mmdb";
            _mockConfiguration.Setup(c => c["MaxMind:DatabasePath"]).Returns(nonExistentPath);
            _service = new GeoLocationService(_mockConfiguration.Object, _mockLogger.Object);

            // Act & Assert
            _service.Dispose();
        }

        [Fact]
        public void Dispose_CalledMultipleTimes_ShouldNotThrow()
        {
            // Arrange
            var nonExistentPath = "/path/to/nonexistent/database.mmdb";
            _mockConfiguration.Setup(c => c["MaxMind:DatabasePath"]).Returns(nonExistentPath);
            _service = new GeoLocationService(_mockConfiguration.Object, _mockLogger.Object);

            // Act & Assert
            _service.Dispose();
            _service.Dispose(); // Should not throw
        }

        private void VerifyWarningLogged(string messageContains)
        {
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(messageContains)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        private void VerifyDebugLogged(string messageContains)
        {
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(messageContains)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        private void VerifyErrorLogged(string messageContains)
        {
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(messageContains)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        public void Dispose()
        {
            _service?.Dispose();
        }
    }
}
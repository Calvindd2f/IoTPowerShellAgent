using System;
using FluentAssertions;
using IoTPowerShellAgent.Utilities;
using Xunit;

namespace IoTPowerShellAgent.Tests.Utilities
{
    /// <summary>
    /// Unit tests for WindowsApiInterop
    /// </summary>
    public class WindowsApiInteropTests
    {
        [Fact]
        public void GetHighResolutionTimestamp_ReturnsValidDateTime()
        {
            // Act
            var timestamp = WindowsApiInterop.GetHighResolutionTimestamp();

            // Assert
            timestamp.Should().BeAfter(DateTime.MinValue);
            timestamp.Should().BeBefore(DateTime.MaxValue);
            timestamp.Kind.Should().Be(DateTimeKind.Utc);
        }

        [Fact]
        public void GetEnvironmentVariableNative_NonExistentVariable_ReturnsNull()
        {
            // Act
            var result = WindowsApiInterop.GetEnvironmentVariableNative("NON_EXISTENT_VAR_12345");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void FileTimeToLong_ValidFileTime_ConvertsCorrectly()
        {
            // Arrange
            var fileTime = new System.Runtime.InteropServices.ComTypes.FILETIME
            {
                dwLowDateTime = 100,
                dwHighDateTime = 0
            };

            // Act
            var result = WindowsApiInterop.FileTimeToLong(fileTime);

            // Assert
            result.Should().Be(100L);
        }

        [Fact]
        public void FileTimeToTimeSpan_ValidFileTime_ConvertsCorrectly()
        {
            // Arrange
            var fileTime = new System.Runtime.InteropServices.ComTypes.FILETIME
            {
                dwLowDateTime = 10000000, // 1 second in 100-nanosecond intervals
                dwHighDateTime = 0
            };

            // Act
            var result = WindowsApiInterop.FileTimeToTimeSpan(fileTime);

            // Assert
            result.TotalSeconds.Should().BeApproximately(1.0, 0.1);
        }
    }
}


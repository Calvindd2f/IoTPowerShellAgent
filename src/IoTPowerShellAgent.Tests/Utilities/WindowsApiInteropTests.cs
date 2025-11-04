using System;
using FluentAssertions;
using IoTPowerShellAgent.Utilities;
using Xunit;

namespace IoTPowerShellAgent.Tests.Utilities
{



    public class WindowsApiInteropTests
    {
        [Fact]
        public void GetHighResolutionTimestamp_ReturnsValidDateTime()
        {

            var timestamp = WindowsApiInterop.GetHighResolutionTimestamp();


            timestamp.Should().BeAfter(DateTime.MinValue);
            timestamp.Should().BeBefore(DateTime.MaxValue);
            timestamp.Kind.Should().Be(DateTimeKind.Utc);
        }

        [Fact]
        public void GetEnvironmentVariableNative_NonExistentVariable_ReturnsNull()
        {

            var result = WindowsApiInterop.GetEnvironmentVariableNative("NON_EXISTENT_VAR_12345");


            result.Should().BeNull();
        }

        [Fact]
        public void FileTimeToLong_ValidFileTime_ConvertsCorrectly()
        {

            var fileTime = new System.Runtime.InteropServices.ComTypes.FILETIME
            {
                dwLowDateTime = 100,
                dwHighDateTime = 0
            };


            var result = WindowsApiInterop.FileTimeToLong(fileTime);


            result.Should().Be(100L);
        }

        [Fact]
        public void FileTimeToTimeSpan_ValidFileTime_ConvertsCorrectly()
        {

            var fileTime = new System.Runtime.InteropServices.ComTypes.FILETIME
            {
                dwLowDateTime = 10000000,
                dwHighDateTime = 0
            };


            var result = WindowsApiInterop.FileTimeToTimeSpan(fileTime);


            result.TotalSeconds.Should().BeApproximately(1.0, 0.1);
        }
    }
}


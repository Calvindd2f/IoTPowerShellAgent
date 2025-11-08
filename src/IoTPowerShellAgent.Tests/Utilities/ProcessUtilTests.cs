using FluentAssertions;
using IoTPowerShellAgent.Utilities;
using Xunit;

namespace IoTPowerShellAgent.Tests.Utilities
{
    /// <summary>
    /// Unit tests for ProcessUtil
    /// </summary>
    public class ProcessUtilTests
    {
        [Fact]
        public void GetMemoryUsageMB_ReturnsPositiveValue()
        {
            // Act
            var memoryMB = ProcessUtil.GetMemoryUsageMB();

            // Assert
            memoryMB.Should().BeGreaterThan(0);
        }

        [Fact]
        public void GetDetailedMemoryInfo_ReturnsValidValues()
        {
            // Act
            var (workingSetMB, privateMB, peakWorkingSetMB) = ProcessUtil.GetDetailedMemoryInfo();

            // Assert
            workingSetMB.Should().BeGreaterThan(0);
            privateMB.Should().BeGreaterThan(0);
            peakWorkingSetMB.Should().BeGreaterThanOrEqualTo(workingSetMB);
        }

        [Fact]
        public void GetCpuUsage_ReturnsValidPercentage()
        {
            // Act
            var cpuUsage = ProcessUtil.GetCpuUsage();

            // Assert
            // First call may return 0.0 (needs baseline), subsequent calls should return valid percentage
            cpuUsage.Should().BeGreaterThanOrEqualTo(0.0);
            cpuUsage.Should().BeLessThanOrEqualTo(100.0);
        }

        [Fact]
        public void SetProcessPriority_ValidPriority_ReturnsTrue()
        {
            // Act
            var result = ProcessUtil.SetProcessPriority(WindowsApiInterop.NORMAL_PRIORITY_CLASS);

            // Assert
            result.Should().BeTrue();
        }
    }
}


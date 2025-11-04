using FluentAssertions;
using IoTPowerShellAgent.Utilities;
using Xunit;

namespace IoTPowerShellAgent.Tests.Utilities
{



    public class ProcessUtilTests
    {
        [Fact]
        public void GetMemoryUsageMB_ReturnsPositiveValue()
        {

            var memoryMB = ProcessUtil.GetMemoryUsageMB();


            memoryMB.Should().BeGreaterThan(0);
        }

        [Fact]
        public void GetDetailedMemoryInfo_ReturnsValidValues()
        {

            var (workingSetMB, privateMB, peakWorkingSetMB) = ProcessUtil.GetDetailedMemoryInfo();


            workingSetMB.Should().BeGreaterThan(0);
            privateMB.Should().BeGreaterThan(0);
            peakWorkingSetMB.Should().BeGreaterThanOrEqualTo(workingSetMB);
        }

        [Fact]
        public void GetCpuUsage_ReturnsValidPercentage()
        {

            var cpuUsage = ProcessUtil.GetCpuUsage();



            cpuUsage.Should().BeGreaterThanOrEqualTo(0.0);
            cpuUsage.Should().BeLessThanOrEqualTo(100.0);
        }

        [Fact]
        public void SetProcessPriority_ValidPriority_ReturnsTrue()
        {

            var result = ProcessUtil.SetProcessPriority(WindowsApiInterop.NORMAL_PRIORITY_CLASS);


            result.Should().BeTrue();
        }
    }
}


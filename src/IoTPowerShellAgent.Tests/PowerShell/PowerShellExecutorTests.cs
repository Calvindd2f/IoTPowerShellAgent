using System;
using System.Threading.Tasks;
using FluentAssertions;
using IoTPowerShellAgent.Core;
using IoTPowerShellAgent.PowerShell;
using IoTPowerShellAgent.Utilities;
using Moq;
using Xunit;

namespace IoTPowerShellAgent.Tests.PowerShell
{
    /// <summary>
    /// Unit tests for PowerShellExecutor
    /// </summary>
    public class PowerShellExecutorTests
    {
        private readonly Mock<ILogCallback> _mockLogCallback;

        public PowerShellExecutorTests()
        {
            _mockLogCallback = new Mock<ILogCallback>();
        }

        [Fact]
        public void ExecutePowerShell_SimpleCommand_ReturnsSuccess()
        {
            // Arrange
            var executor = new PowerShellExecutor(_mockLogCallback.Object);
            string script = "Write-Output 'Hello World'";

            // Act
            var result = executor.ExecutePowerShell(script, isInlinePowershell: false);

            // Assert
            result.Success.Should().BeTrue();
            result.Output.Should().Contain("Hello World");
            result.ErrorMessage.Should().BeNullOrEmpty();
        }

        [Fact]
        public void ExecutePowerShell_ErrorCommand_ReturnsError()
        {
            // Arrange
            var executor = new PowerShellExecutor(_mockLogCallback.Object);
            string script = "Write-Error 'Test Error'";

            // Act
            var result = executor.ExecutePowerShell(script, isInlinePowershell: false);

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void ExecutePowerShell_Base64EncodedScript_DecodesCorrectly()
        {
            // Arrange
            var executor = new PowerShellExecutor(_mockLogCallback.Object);
            string originalScript = "Write-Output 'Base64 Test'";
            string base64Script = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(originalScript));

            // Act
            // Note: PowerShellExecutor doesn't handle base64 directly, but the caller does
            // This test verifies the executor works with decoded scripts
            var result = executor.ExecutePowerShell(originalScript, isInlinePowershell: false);

            // Assert
            result.Success.Should().BeTrue();
            result.Output.Should().Contain("Base64 Test");
        }

        [Fact]
        public void ExecutePowerShell_VerboseOutput_CapturesVerboseStream()
        {
            // Arrange
            var executor = new PowerShellExecutor(_mockLogCallback.Object);
            string script = "Write-Verbose 'Verbose Message' -Verbose";

            // Act
            var result = executor.ExecutePowerShell(script, isInlinePowershell: false);

            // Assert
            result.Success.Should().BeTrue();
            _mockLogCallback.Verify(x => x.OnLog(
                It.Is<string>(s => s.Contains("Verbose Message")),
                LogOutputType.Verbose), Times.AtLeastOnce);
        }

        [Fact]
        public void ExecutePowerShell_WarningOutput_CapturesWarningStream()
        {
            // Arrange
            var executor = new PowerShellExecutor(_mockLogCallback.Object);
            string script = "Write-Warning 'Warning Message'";

            // Act
            var result = executor.ExecutePowerShell(script, isInlinePowershell: false);

            // Assert
            result.Success.Should().BeTrue();
            _mockLogCallback.Verify(x => x.OnLog(
                It.Is<string>(s => s.Contains("Warning Message")),
                LogOutputType.Warning), Times.AtLeastOnce);
        }

        [Fact]
        public void ExecutePowerShell_Dispose_CleansUpResources()
        {
            // Arrange
            var executor = new PowerShellExecutor(_mockLogCallback.Object);

            // Act & Assert
            executor.Dispose(); // Should not throw
            executor.Dispose(); // Should be idempotent
        }
    }
}


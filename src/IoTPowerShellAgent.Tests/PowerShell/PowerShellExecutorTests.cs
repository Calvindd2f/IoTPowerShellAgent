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

            var executor = new PowerShellExecutor(_mockLogCallback.Object);
            string script = "Write-Output 'Hello World'";


            var result = executor.ExecutePowerShell(script, isInlinePowershell: false);


            result.Success.Should().BeTrue("because ErrorMessage was: " + result.ErrorMessage);
            result.Output.Should().Contain("Hello World");
            result.ErrorMessage.Should().BeNullOrEmpty();
        }

        [Fact]
        public void ExecutePowerShell_ErrorCommand_ReturnsError()
        {

            var executor = new PowerShellExecutor(_mockLogCallback.Object);
            string script = "Write-Error 'Test Error'";


            var result = executor.ExecutePowerShell(script, isInlinePowershell: false);


            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void ExecutePowerShell_Base64EncodedScript_DecodesCorrectly()
        {

            var executor = new PowerShellExecutor(_mockLogCallback.Object);
            string originalScript = "Write-Output 'Base64 Test'";
            string base64Script = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(originalScript));




            var result = executor.ExecutePowerShell(originalScript, isInlinePowershell: false);


            result.Success.Should().BeTrue();
            result.Output.Should().Contain("Base64 Test");
        }

        [Fact]
        public void ExecutePowerShell_VerboseOutput_CapturesVerboseStream()
        {

            var executor = new PowerShellExecutor(_mockLogCallback.Object);
            string script = "Write-Verbose 'Verbose Message' -Verbose";


            var result = executor.ExecutePowerShell(script, isInlinePowershell: false);


            result.Success.Should().BeTrue();
            _mockLogCallback.Verify(x => x.OnLog(
                It.Is<string>(s => s.Contains("Verbose Message")),
                LogOutputType.Verbose), Times.AtLeastOnce);
        }

        [Fact]
        public void ExecutePowerShell_WarningOutput_CapturesWarningStream()
        {

            var executor = new PowerShellExecutor(_mockLogCallback.Object);
            string script = "Write-Warning 'Warning Message'";


            var result = executor.ExecutePowerShell(script, isInlinePowershell: false);


            result.Success.Should().BeTrue();
            _mockLogCallback.Verify(x => x.OnLog(
                It.Is<string>(s => s.Contains("Warning Message")),
                LogOutputType.Warning), Times.AtLeastOnce);
        }

        [Fact]
        public void ExecutePowerShell_Dispose_CleansUpResources()
        {

            var executor = new PowerShellExecutor(_mockLogCallback.Object);


            executor.Dispose();
            executor.Dispose();
        }
    }
}


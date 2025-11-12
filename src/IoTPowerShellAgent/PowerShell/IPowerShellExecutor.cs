using System;
using System.Threading;
using System.Threading.Tasks;
using IoTPowerShellAgent.Core;

namespace IoTPowerShellAgent.PowerShell
{
    /// <summary>
    /// Interface for PowerShell script execution
    /// Enables unit testing with mocks
    /// </summary>
    public interface IPowerShellExecutor : IDisposable
    {
        /// <summary>
        /// Executes PowerShell script synchronously
        /// </summary>
        PowerShellExecutionResult ExecutePowerShell(string script, bool isInlinePowershell);

        /// <summary>
        /// Executes PowerShell script asynchronously with cancellation token support
        /// </summary>
        Task<PowerShellExecutionResult> ExecutePowerShellAsync(string script, bool isInlinePowershell, CancellationToken cancellationToken = default);
    }
}


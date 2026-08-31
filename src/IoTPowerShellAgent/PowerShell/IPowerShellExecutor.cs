using System;
using System.Threading;
using System.Threading.Tasks;
using IoTPowerShellAgent.Core;

namespace IoTPowerShellAgent.PowerShell
{




    public interface IPowerShellExecutor : IDisposable
    {



        PowerShellExecutionResult ExecutePowerShell(string script, bool isInlinePowershell);




        Task<PowerShellExecutionResult> ExecutePowerShellAsync(string script, bool isInlinePowershell, CancellationToken cancellationToken = default);
    }
}


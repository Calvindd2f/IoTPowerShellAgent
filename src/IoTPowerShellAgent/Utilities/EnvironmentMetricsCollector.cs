using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IoTPowerShellAgent.Core;
using IoTPowerShellAgent.PowerShell;

namespace IoTPowerShellAgent.Utilities
{
    /// <summary>
    /// Collects environment metrics
    /// </summary>
    public class EnvironmentMetricsCollector
    {
        private readonly ILogCallback? _logCallback;
        private readonly PowerShellExecutor _executor;

        public EnvironmentMetricsCollector(ILogCallback? logCallback = null)
        {
            _logCallback = logCallback;
            _executor = new PowerShellExecutor(logCallback);
        }

        /// <summary>
        /// Gets the AD domain using PowerShell/WMI
        /// </summary>
        public async Task<string?> GetAdDomainAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                string script = @"
$domainInfo = (Get-WmiObject Win32_ComputerSystem).Domain
if ($domainInfo -and $domainInfo -ne 'WORKGROUP') {
    return $domainInfo
} else {
    return $null
}";

                var result = await _executor.ExecutePowerShellAsync(script, isInlinePowershell: false, cancellationToken).ConfigureAwait(false);

                if (result.Success && !string.IsNullOrWhiteSpace(result.Output))
                {
                    string domain = result.Output.Trim();
                    if (!string.IsNullOrEmpty(domain) && !domain.Equals("WORKGROUP", StringComparison.OrdinalIgnoreCase))
                    {
                        _logCallback?.OnLog($"AD Domain: {domain}", LogOutputType.Information);
                        return domain;
                    }
                }
            }
            catch (Exception ex)
            {
                _logCallback?.OnLog($"Error getting AD domain: {ex.Message}", LogOutputType.Warning);
            }

            return null;
        }

        /// <summary>
        /// Checks if the machine is an AD domain controller using PowerShell/WMI
        /// </summary>
        public async Task<bool> GetIsAdDomainControllerAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                string script = @"
$domainStatus = (Get-WmiObject Win32_ComputerSystem).DomainRole
if ($domainStatus -eq 4 -or $domainStatus -eq 5) {
    return $true
} else {
    return $false
}";

                var result = await _executor.ExecutePowerShellAsync(script, isInlinePowershell: false, cancellationToken).ConfigureAwait(false);

                if (result.Success && !string.IsNullOrWhiteSpace(result.Output))
                {
                    string output = result.Output.Trim();
                    bool isDomainController = output.Equals("True", StringComparison.OrdinalIgnoreCase);
                    _logCallback?.OnLog($"Is AD Domain Controller: {isDomainController}", LogOutputType.Information);
                    return isDomainController;
                }
            }
            catch (Exception ex)
            {
                _logCallback?.OnLog($"Error checking domain controller status: {ex.Message}", LogOutputType.Warning);
            }

            return false;
        }

        /// <summary>
        /// Checks if Entra Connect services are running
        /// </summary>
        public bool GetIsEntraConnectServer()
        {
            try
            {
                string[] entraServiceNames = { "ADSync", "Azure AD Sync", "EntraConnectSync", "OtherFutureName" };

                ServiceController[] services = ServiceController.GetServices();

                foreach (var service in services)
                {
                    foreach (var entraServiceName in entraServiceNames)
                    {
                        if (service.ServiceName.Equals(entraServiceName, StringComparison.OrdinalIgnoreCase))
                        {
                            _logCallback?.OnLog($"Entra Connect service found: {service.ServiceName}", LogOutputType.Information);
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logCallback?.OnLog($"Error checking Entra Connect services: {ex.Message}", LogOutputType.Warning);
            }

            return false;
        }

        /// <summary>
        /// Gets MAC address from network interfaces
        /// </summary>
        public string? GetMacAddress()
        {
            try
            {
                NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();

                foreach (var iface in interfaces)
                {
                    PhysicalAddress? macAddress = iface.GetPhysicalAddress();
                    if (macAddress != null && macAddress.ToString().Length > 0)
                    {
                        // Replace : with empty string
                        string mac = macAddress.ToString().Replace(":", "");
                        _logCallback?.OnLog($"MAC Address: {mac}", LogOutputType.Information);
                        return mac;
                    }
                }
            }
            catch (Exception ex)
            {
                _logCallback?.OnLog($"Error getting MAC address: {ex.Message}", LogOutputType.Warning);
            }

            _logCallback?.OnLog("No MAC address found", LogOutputType.Warning);
            return null;
        }

        /// <summary>
        /// Gets Entra domain from dsregcmd output
        /// </summary>
        public async Task<string?> GetEntraDomainAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "dsregcmd",
                    Arguments = "/status",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = processStartInfo };
                var outputBuilder = new StringBuilder();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        outputBuilder.AppendLine(e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();

                // Wait for process to exit (with cancellation support)
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                string output = outputBuilder.ToString();
                bool azureAdJoined = false;
                string? domain = null;

                foreach (string line in output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.Contains("AzureAdJoined", StringComparison.OrdinalIgnoreCase) &&
                        line.Contains("YES", StringComparison.OrdinalIgnoreCase))
                    {
                        azureAdJoined = true;
                    }

                    if (line.Contains("DomainName", StringComparison.OrdinalIgnoreCase))
                    {
                        string[] parts = line.Split(':');
                        if (parts.Length > 1)
                        {
                            domain = parts[1].Trim();
                        }

                        if (azureAdJoined && !string.IsNullOrEmpty(domain))
                        {
                            _logCallback?.OnLog($"Entra Domain: {domain}", LogOutputType.Information);
                            return domain;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logCallback?.OnLog($"Error getting Entra domain: {ex.Message}", LogOutputType.Warning);
            }

            return null;
        }

        /// <summary>
        /// Collects all environment metrics and returns them as a dictionary
        /// </summary>
        public async Task<Dictionary<string, object?>> CollectAllMetricsAsync(CancellationToken cancellationToken = default)
        {
            var metrics = new Dictionary<string, object?>();

            try
            {
                var adDomainTask = GetAdDomainAsync(cancellationToken);
                var isDomainControllerTask = GetIsAdDomainControllerAsync(cancellationToken);
                var entraDomainTask = GetEntraDomainAsync(cancellationToken);

                // Run PowerShell queries in parallel
                await Task.WhenAll(adDomainTask, isDomainControllerTask, entraDomainTask).ConfigureAwait(false);

                metrics["adDomain"] = await adDomainTask.ConfigureAwait(false);
                metrics["isAdDomainController"] = await isDomainControllerTask.ConfigureAwait(false);
                metrics["isEntraConnectServer"] = GetIsEntraConnectServer();
                metrics["macAddress"] = GetMacAddress();
                metrics["entraDomain"] = await entraDomainTask.ConfigureAwait(false);
                metrics["collectedAt"] = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logCallback?.OnLog($"Error collecting environment metrics: {ex.Message}", LogOutputType.Error);
                metrics["error"] = ex.Message;
            }

            return metrics;
        }

        /// <summary>
        /// Logs all collected metrics
        /// </summary>
        public async Task LogAllMetricsAsync(CancellationToken cancellationToken = default)
        {
            var metrics = await CollectAllMetricsAsync(cancellationToken).ConfigureAwait(false);

            _logCallback?.OnLog("=== Environment Metrics ===", LogOutputType.Information);
            foreach (var metric in metrics)
            {
                if (metric.Key != "collectedAt")
                {
                    _logCallback?.OnLog($"{metric.Key}: {metric.Value ?? "null"}", LogOutputType.Information);
                }
            }
            _logCallback?.OnLog($"Collected at: {metrics.GetValueOrDefault("collectedAt")}", LogOutputType.Information);
        }
    }
}


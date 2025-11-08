using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using IoTPowerShellAgent.Core;
using IoTPowerShellAgent.IoT;
using IoTPowerShellAgent.PowerShell;
using IoTPowerShellAgent.Utilities;

namespace IoTPowerShellAgent.TestHelpers
{
    /// <summary>
    /// Helper class for local testing of PowerShell execution without IoT Hub
    /// </summary>
    public class LocalIoTTester
    {
        private readonly TestLogCallback _logCallback;

        public LocalIoTTester()
        {
            _logCallback = new TestLogCallback();
        }

        /// <summary>
        /// Simulates an IoT Hub direct method call locally
        /// </summary>
        public Task<ScriptExecutionResponse> ExecuteScriptLocally(string script, bool isInlinePowershell = false, bool isBase64Encoded = false)
        {
            Console.WriteLine("=== Local IoT Hub Test Execution ===");
            Console.WriteLine($"Script (Length: {script.Length}): {script.Substring(0, Math.Min(100, script.Length))}...");
            Console.WriteLine($"IsInlinePowershell: {isInlinePowershell}, IsBase64Encoded: {isBase64Encoded}");
            Console.WriteLine();

            // Create a new executor with a capturing log callback for this execution
            var capturingCallback = new CapturingLogCallback();
            var executor = new PowerShellExecutor(capturingCallback);

            try
            {
                // Handle base64 decoding if needed
                string decodedScript = script;
                if (isBase64Encoded || IsBase64String(script))
                {
                    try
                    {
                        byte[] bytes = Convert.FromBase64String(script);
                        decodedScript = Encoding.UTF8.GetString(bytes);
                        Console.WriteLine("✓ Script decoded from Base64");
                    }
                    catch (FormatException)
                    {
                        Console.WriteLine("⚠ Base64 decode failed, using script as-is");
                    }
                }

                Console.WriteLine($"Executing script...");
                var result = executor.ExecutePowerShell(decodedScript, isInlinePowershell);

                // Combine pipeline output with captured log output (for host-output commands like Format-Table)
                string combinedOutput = result.Output;
                string capturedOutput = capturingCallback.GetCapturedOutput();
                
                if (!string.IsNullOrEmpty(capturedOutput))
                {
                    if (!string.IsNullOrEmpty(combinedOutput))
                    {
                        combinedOutput = combinedOutput + Environment.NewLine + capturedOutput;
                    }
                    else
                    {
                        combinedOutput = capturedOutput;
                    }
                }

                var response = new ScriptExecutionResponse
                {
                    Success = result.Success,
                    Output = combinedOutput,
                    ErrorMessage = result.ErrorMessage
                };

                Console.WriteLine();
                Console.WriteLine("=== Execution Result ===");
                Console.WriteLine($"Success: {response.Success}");
                if (!string.IsNullOrEmpty(response.Output))
                {
                    Console.WriteLine($"Output:");
                    Console.WriteLine(response.Output);
                }
                else
                {
                    Console.WriteLine($"Output: (empty)");
                }
                if (!string.IsNullOrEmpty(response.ErrorMessage))
                {
                    Console.WriteLine($"Error: {response.ErrorMessage}");
                }

                executor.Dispose();
                return Task.FromResult(response);
            }
            catch (Exception ex)
            {
                executor.Dispose();
                Console.WriteLine($"Exception: {ex}");
                return Task.FromResult(new ScriptExecutionResponse
                {
                    Success = false,
                    ErrorMessage = ex.ToString()
                });
            }
        }

        /// <summary>
        /// Tests with a sample PowerShell script
        /// </summary>
        public Task TestWithSampleScript()
        {
            Console.WriteLine("\n=== Testing with Sample Script ===");
            string sampleScript = "Get-Process | Select-Object -First 3 Name, CPU, WorkingSet | Format-Table";

            return ExecuteScriptLocally(sampleScript, isInlinePowershell: false);
        }

        /// <summary>
        /// Tests with a base64 encoded script
        /// </summary>
        public Task TestWithBase64Script()
        {
            Console.WriteLine("\n=== Testing with Base64 Encoded Script ===");
            string script = "Write-Host 'Hello from Base64!' ; Get-Date";
            string base64Script = Convert.ToBase64String(Encoding.UTF8.GetBytes(script));

            return ExecuteScriptLocally(base64Script, isInlinePowershell: false, isBase64Encoded: true);
        }

        /// <summary>
        /// Tests environment metrics collection
        /// </summary>
        public async Task TestEnvironmentMetrics()
        {
            Console.WriteLine("\n=== Testing Environment Metrics Collection ===");

            var metricsCollector = new EnvironmentMetricsCollector(_logCallback);

            try
            {
                Console.WriteLine("Collecting environment metrics...");
                await metricsCollector.LogAllMetricsAsync().ConfigureAwait(false);

                Console.WriteLine("\n=== Individual Metric Tests ===");

                var adDomain = await metricsCollector.GetAdDomainAsync().ConfigureAwait(false);
                Console.WriteLine($"AD Domain: {adDomain ?? "null"}");

                var isDomainController = await metricsCollector.GetIsAdDomainControllerAsync().ConfigureAwait(false);
                Console.WriteLine($"Is Domain Controller: {isDomainController}");

                var isEntraConnect = metricsCollector.GetIsEntraConnectServer();
                Console.WriteLine($"Is Entra Connect Server: {isEntraConnect}");

                var macAddress = metricsCollector.GetMacAddress();
                Console.WriteLine($"MAC Address: {macAddress ?? "null"}");

                var entraDomain = await metricsCollector.GetEntraDomainAsync().ConfigureAwait(false);
                Console.WriteLine($"Entra Domain: {entraDomain ?? "null"}");

                Console.WriteLine("\n✓ Environment metrics collection completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error collecting metrics: {ex}");
            }
        }

        private static bool IsBase64String(string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                return false;

            if (str.Length < 20 || str.Length % 4 != 0)
                return false;

            try
            {
                Convert.FromBase64String(str);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private class TestLogCallback : ILogCallback
        {
            public void OnLog(string message, LogOutputType logType)
            {
                Console.WriteLine($"[{logType}] {message}");
            }
        }

        /// <summary>
        /// Log callback that captures output for commands that output to host (like Format-Table)
        /// </summary>
        private class CapturingLogCallback : ILogCallback
        {
            private readonly StringBuilder _outputBuilder = new StringBuilder();

            public void OnLog(string message, LogOutputType logType)
            {
                // Capture Information, Debug, Verbose, and Warning output
                // Format-Table outputs to Information stream
                // Debug messages help troubleshoot serialization issues
                if (logType == LogOutputType.Information || 
                    logType == LogOutputType.Verbose || 
                    logType == LogOutputType.Debug)
                {
                    if (_outputBuilder.Length > 0)
                    {
                        _outputBuilder.AppendLine();
                    }
                    // Prefix debug messages for clarity
                    if (logType == LogOutputType.Debug)
                    {
                        _outputBuilder.Append($"[DEBUG] {message}");
                    }
                    else
                    {
                        _outputBuilder.Append(message);
                    }
                }
                else if (logType == LogOutputType.Warning)
                {
                    // Include warnings but mark them
                    if (_outputBuilder.Length > 0)
                    {
                        _outputBuilder.AppendLine();
                    }
                    _outputBuilder.Append($"Warning: {message}");
                }
            }

            public string GetCapturedOutput()
            {
                return _outputBuilder.ToString();
            }
        }
    }
}

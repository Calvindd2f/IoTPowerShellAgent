using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using IoTPowerShellAgent.Core;
using IoTPowerShellAgent.IoT;
using IoTPowerShellAgent.PowerShell;

namespace IoTPowerShellAgent.TestHelpers
{
    /// <summary>
    /// Helper class for local testing of PowerShell execution without IoT Hub
    /// </summary>
    public class LocalIoTTester
    {
        private readonly PowerShellExecutor _executor;
        private readonly TestLogCallback _logCallback;

        public LocalIoTTester()
        {
            _logCallback = new TestLogCallback();
            _executor = new PowerShellExecutor(_logCallback);
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
                var result = _executor.ExecutePowerShell(decodedScript, isInlinePowershell);

                var response = new ScriptExecutionResponse
                {
                    Success = result.Success,
                    Output = result.Output,
                    ErrorMessage = result.ErrorMessage
                };

                Console.WriteLine();
                Console.WriteLine("=== Execution Result ===");
                Console.WriteLine($"Success: {response.Success}");
                Console.WriteLine($"Output: {response.Output}");
                if (!string.IsNullOrEmpty(response.ErrorMessage))
                {
                    Console.WriteLine($"Error: {response.ErrorMessage}");
                }

                return Task.FromResult(response);
            }
            catch (Exception ex)
            {
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
    }
}

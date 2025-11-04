using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using IoTPowerShellAgent.Core;
using IoTPowerShellAgent.PowerShell;

namespace IoTPowerShellAgent.IoT
{
    /// <summary>
    /// Service for Azure IoT Hub integration
    /// </summary>
    public class IoTHubService : IDisposable, ILogCallback
    {
        private DeviceClient? _deviceClient;
        private ModuleClient? _moduleClient;
        private readonly SettingsService _settings;
        private bool _disposed = false;

        public IoTHubService()
        {
            _settings = SettingsService.Instance;
        }

        /// <summary>
        /// Connects to Azure IoT Hub
        /// </summary>
        public async Task ConnectAsync()
        {
            if (!string.IsNullOrEmpty(_settings.Settings.ModuleId))
            {
                // Use ModuleClient for IoT Edge modules
                _moduleClient = ModuleClient.CreateFromConnectionString(
                    _settings.Settings.IoTHubConnectionString,
                    Microsoft.Azure.Devices.Client.TransportType.Amqp);
                await _moduleClient.OpenAsync();

                await _moduleClient.SetMethodHandlerAsync("ExecuteScript", ExecuteScriptMethodHandler, null);
                await _moduleClient.SetInputMessageHandlerAsync("scriptInput", HandleInputMessage, null);
            }
            else
            {
                // Use DeviceClient for regular devices
                _deviceClient = DeviceClient.CreateFromConnectionString(
                    _settings.Settings.IoTHubConnectionString,
                    Microsoft.Azure.Devices.Client.TransportType.Amqp);
                await _deviceClient.OpenAsync();

                await _deviceClient.SetMethodHandlerAsync("ExecuteScript", ExecuteScriptMethodHandler, null);
            }
        }

        /// <summary>
        /// Handles direct method invocation for script execution
        /// </summary>
        private async Task<MethodResponse> ExecuteScriptMethodHandler(MethodRequest methodRequest, object userContext)
        {
            try
            {
                var request = JsonSerializer.Deserialize<ScriptExecutionRequest>(methodRequest.DataAsJson);
                if (request == null || string.IsNullOrEmpty(request.Script))
                {
                    return new MethodResponse(400);
                }

                // Handle base64 encoded scripts (optional - IoT Hub doesn't require it, but supports it)
                string script = request.Script;
                if (request.IsBase64Encoded || IsBase64String(script))
                {
                    try
                    {
                        byte[] bytes = Convert.FromBase64String(script);
                        script = Encoding.UTF8.GetString(bytes);
                    }
                    catch (FormatException)
                    {
                        // If base64 decode fails, use script as-is
                    }
                }

                var executor = new IoTPowerShellAgent.PowerShell.PowerShellExecutor(this);
                var result = executor.ExecutePowerShell(script, request.IsInlinePowershell);

                var response = new ScriptExecutionResponse
                {
                    Success = result.Success,
                    Output = result.Output,
                    ErrorMessage = result.ErrorMessage
                };

                string responseJson = JsonSerializer.Serialize(response);
                return new MethodResponse(Encoding.UTF8.GetBytes(responseJson), 200);
            }
            catch (Exception ex)
            {
                var errorResponse = new ScriptExecutionResponse
                {
                    Success = false,
                    ErrorMessage = ex.ToString()
                };
                string responseJson = JsonSerializer.Serialize(errorResponse);
                return new MethodResponse(Encoding.UTF8.GetBytes(responseJson), 500);
            }
        }

        /// <summary>
        /// Checks if a string is base64 encoded (basic heuristic)
        /// </summary>
        private static bool IsBase64String(string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                return false;

            // Base64 strings are typically longer and contain only base64 characters
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

        /// <summary>
        /// Handles input messages from IoT Hub
        /// </summary>
        private Task<MessageResponse> HandleInputMessage(Message message, object userContext)
        {
            try
            {
                string messageBody = Encoding.UTF8.GetString(message.GetBytes());
                var request = JsonSerializer.Deserialize<ScriptExecutionRequest>(messageBody);

                if (request != null && !string.IsNullOrEmpty(request.Script))
                {
                    var executor = new IoTPowerShellAgent.PowerShell.PowerShellExecutor(this);
                    executor.ExecutePowerShell(request.Script, request.IsInlinePowershell);
                }

                return Task.FromResult(MessageResponse.Completed);
            }
            catch (Exception ex)
            {
                OnLog($"Error processing input message: {ex}", LogOutputType.Error);
                return Task.FromResult(MessageResponse.Abandoned);
            }
        }

        /// <summary>
        /// Sends telemetry data to IoT Hub
        /// </summary>
        public async Task SendTelemetryAsync(object telemetryData)
        {
            try
            {
                string json = JsonSerializer.Serialize(telemetryData);
                var message = new Message(Encoding.UTF8.GetBytes(json));

                if (_moduleClient != null)
                {
                    await _moduleClient.SendEventAsync(message);
                }
                else if (_deviceClient != null)
                {
                    await _deviceClient.SendEventAsync(message);
                }
            }
            catch (Exception ex)
            {
                OnLog($"Error sending telemetry: {ex}", LogOutputType.Error);
            }
        }

        /// <summary>
        /// Updates device twin reported properties
        /// </summary>
        public async Task UpdateTwinAsync(TwinCollection reportedProperties)
        {
            try
            {
                if (_moduleClient != null)
                {
                    await _moduleClient.UpdateReportedPropertiesAsync(reportedProperties);
                }
                else if (_deviceClient != null)
                {
                    await _deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
                }
            }
            catch (Exception ex)
            {
                OnLog($"Error updating twin: {ex}", LogOutputType.Error);
            }
        }

        /// <summary>
        /// ILogCallback implementation - sends logs as telemetry
        /// </summary>
        public void OnLog(string message, LogOutputType logType)
        {
            try
            {
                var logTelemetry = new
                {
                    timestamp = DateTime.UtcNow,
                    logType = logType.ToString(),
                    message = message,
                    deviceId = _settings.Settings.DeviceId
                };

                // Send as telemetry in background
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await SendTelemetryAsync(logTelemetry);
                    }
                    catch
                    {
                        // Silently fail to avoid blocking
                    }
                });
            }
            catch
            {
                // Silently fail to avoid blocking
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _deviceClient?.Dispose();
                _moduleClient?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Request model for script execution
    /// </summary>
    public class ScriptExecutionRequest
    {
        public string Script { get; set; } = string.Empty;
        public bool IsInlinePowershell { get; set; } = false;
        public bool IsBase64Encoded { get; set; } = false; // Added for base64 support
    }

    /// <summary>
    /// Response model for script execution
    /// </summary>
    public class ScriptExecutionResponse
    {
        public bool Success { get; set; }
        public string Output { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }
}

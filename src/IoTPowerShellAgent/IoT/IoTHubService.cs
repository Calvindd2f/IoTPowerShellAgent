using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using IoTPowerShellAgent.Core;
using IoTPowerShellAgent.PowerShell;
using IoTPowerShellAgent.Utilities;

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
        private readonly ILogCallback? _logCallback;
        private bool _disposed = false;

        public IoTHubService(ILogCallback? logCallback = null)
        {
            _settings = SettingsService.Instance;
            _logCallback = logCallback ?? this; // Use this (IoT Hub) as default callback if none provided
        }

        /// <summary>
        /// Connects to Azure IoT Hub
        /// </summary>
        public async Task ConnectAsync()
        {
            var transportType = GetTransportType(_settings.Settings.TransportType);

            if (!string.IsNullOrEmpty(_settings.Settings.ModuleId))
            {
                // Use ModuleClient for IoT Edge modules
                if (!string.IsNullOrEmpty(_settings.Settings.IoTHubConnectionString))
                {
                    _moduleClient = ModuleClient.CreateFromConnectionString(
                        _settings.Settings.IoTHubConnectionString,
                        transportType);
                }
                else
                {
                    // Use individual credentials
                    _moduleClient = CreateModuleClientFromCredentials(transportType);
                }

                await _moduleClient.OpenAsync().ConfigureAwait(false);
                await _moduleClient.SetMethodHandlerAsync("ExecuteScript", ExecuteScriptMethodHandler, null).ConfigureAwait(false);
                await _moduleClient.SetInputMessageHandlerAsync("scriptInput", HandleInputMessage, null).ConfigureAwait(false);
            }
            else
            {
                // Use DeviceClient for regular devices
                if (!string.IsNullOrEmpty(_settings.Settings.IoTHubConnectionString))
                {
                    _deviceClient = DeviceClient.CreateFromConnectionString(
                        _settings.Settings.IoTHubConnectionString,
                        transportType);
                }
                else
                {
                    // Use individual credentials
                    _deviceClient = CreateDeviceClientFromCredentials(transportType);
                }

                await _deviceClient.OpenAsync().ConfigureAwait(false);
                await _deviceClient.SetMethodHandlerAsync("ExecuteScript", ExecuteScriptMethodHandler, null).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Gets the transport type from string configuration
        /// </summary>
        private static TransportType GetTransportType(string transportType)
        {
            return transportType?.ToLowerInvariant() switch
            {
                "mqtt" => TransportType.Mqtt,
                "mqtt_websocket_only" or "mqttwebsocketonly" or "mqtt_ws" => TransportType.Mqtt_WebSocket_Only,
                "amqp_websocket_only" or "amqpwebsocketonly" or "amqp_ws" => TransportType.Amqp_WebSocket_Only,
                "http1" => TransportType.Http1,
                _ => TransportType.Amqp // Default
            };
        }

        /// <summary>
        /// Creates a DeviceClient from individual credentials
        /// </summary>
        private DeviceClient CreateDeviceClientFromCredentials(TransportType transportType)
        {
            if (string.IsNullOrEmpty(_settings.Settings.AzureIotHubHost) ||
                string.IsNullOrEmpty(_settings.Settings.DeviceId) ||
                string.IsNullOrEmpty(_settings.Settings.SharedAccessKey))
            {
                throw new InvalidOperationException(
                    "Either IoTHubConnectionString or AzureIotHubHost, DeviceId, and SharedAccessKey must be provided.");
            }

            // Generate SAS token
            string sasToken = SasTokenGenerator.GenerateDeviceSasToken(
                _settings.Settings.AzureIotHubHost,
                _settings.Settings.DeviceId,
                _settings.Settings.SharedAccessKey,
                TimeSpan.FromHours(1));

            // Create authentication using SAS token
            var authMethod = new DeviceAuthenticationWithToken(
                _settings.Settings.DeviceId,
                sasToken);

            // Create device client
            return DeviceClient.Create(
                _settings.Settings.AzureIotHubHost,
                authMethod,
                transportType);
        }

        /// <summary>
        /// Creates a ModuleClient from individual credentials
        /// </summary>
        private ModuleClient CreateModuleClientFromCredentials(TransportType transportType)
        {
            if (string.IsNullOrEmpty(_settings.Settings.AzureIotHubHost) ||
                string.IsNullOrEmpty(_settings.Settings.DeviceId) ||
                string.IsNullOrEmpty(_settings.Settings.ModuleId) ||
                string.IsNullOrEmpty(_settings.Settings.SharedAccessKey))
            {
                throw new InvalidOperationException(
                    "Either IoTHubConnectionString or AzureIotHubHost, DeviceId, ModuleId, and SharedAccessKey must be provided.");
            }

            // Generate SAS token
            string resourceUri = $"{_settings.Settings.AzureIotHubHost}/devices/{_settings.Settings.DeviceId}/modules/{_settings.Settings.ModuleId}";
            string sasToken = SasTokenGenerator.GenerateSasToken(
                resourceUri,
                _settings.Settings.SharedAccessKey,
                TimeSpan.FromHours(1));

            // Create authentication using SAS token
            var authMethod = new ModuleAuthenticationWithToken(
                _settings.Settings.DeviceId,
                _settings.Settings.ModuleId,
                sasToken);

            // Create module client
            return ModuleClient.Create(
                _settings.Settings.AzureIotHubHost,
                authMethod,
                transportType);
        }

        /// <summary>
        /// Handles direct method invocation for script execution
        /// </summary>
        private async Task<MethodResponse> ExecuteScriptMethodHandler(MethodRequest methodRequest, object userContext)
        {
            // Elevate process priority to HIGH during execution for optimal performance
            // Priority is automatically restored to NORMAL when execution completes
            using (new ProcessPriorityManager())
            {
                // Create cancellation token with timeout from settings
                var settings = SettingsService.Instance.Settings;
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(settings.ScriptTimeoutSeconds));
                
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

                    var executor = new IoTPowerShellAgent.PowerShell.PowerShellExecutor(_logCallback);
                    // Use async execution with cancellation token to prevent blocking IoT listener
                    var result = await executor.ExecutePowerShellAsync(script, request.IsInlinePowershell, cts.Token).ConfigureAwait(false);

                    // Compress Output field if it's large enough to benefit (threshold: 1KB)
                    // Compression typically saves 60-80% for JSON data
                    const int compressionThreshold = 1024; // 1KB
                    var response = new ScriptExecutionResponse
                    {
                        Success = result.Success,
                        Output = result.Output,
                        ErrorMessage = result.ErrorMessage,
                        IsCompressed = false
                    };

                    // Compress the Output field if it's large
                    if (!string.IsNullOrEmpty(result.Output) && result.Output.Length > compressionThreshold)
                    {
                        byte[] outputBytes = Encoding.UTF8.GetBytes(result.Output);
                        byte[] compressedBytes = CompressGZip(outputBytes);
                        
                        // Only use compression if it actually reduces size
                        if (compressedBytes.Length < outputBytes.Length)
                        {
                            response.Output = Convert.ToBase64String(compressedBytes);
                            response.IsCompressed = true;
                            response.OriginalSize = outputBytes.Length;
                            response.CompressedSize = compressedBytes.Length;
                        }
                    }

                    string responseJson = JsonSerializer.Serialize(response);
                    byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);
                    
                    // Priority remains HIGH until this response is returned (HTTP postback)
                    return new MethodResponse(responseBytes, 200);
                }
                catch (OperationCanceledException)
                {
                    var errorResponse = new ScriptExecutionResponse
                    {
                        Success = false,
                        ErrorMessage = $"Script execution was cancelled or timed out after {settings.ScriptTimeoutSeconds} seconds.",
                        IsCompressed = false
                    };

                    string responseJson = JsonSerializer.Serialize(errorResponse);
                    byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);
                    
                    // Priority remains HIGH until this error response is returned
                    return new MethodResponse(responseBytes, 408); // 408 Request Timeout
                }
                catch (Exception ex)
                {
                    var errorResponse = new ScriptExecutionResponse
                    {
                        Success = false,
                        ErrorMessage = ex.ToString(),
                        IsCompressed = false
                    };

                    // Compress ErrorMessage field if it's large
                    const int compressionThreshold = 1024;
                    if (!string.IsNullOrEmpty(errorResponse.ErrorMessage) && errorResponse.ErrorMessage.Length > compressionThreshold)
                    {
                        byte[] errorBytes = Encoding.UTF8.GetBytes(errorResponse.ErrorMessage);
                        byte[] compressedBytes = CompressGZip(errorBytes);
                        
                        if (compressedBytes.Length < errorBytes.Length)
                        {
                            errorResponse.ErrorMessage = Convert.ToBase64String(compressedBytes);
                            errorResponse.IsCompressed = true;
                            errorResponse.OriginalSize = errorBytes.Length;
                            errorResponse.CompressedSize = compressedBytes.Length;
                        }
                    }

                    string responseJson = JsonSerializer.Serialize(errorResponse);
                    byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);
                    
                    // Priority remains HIGH until this error response is returned
                    return new MethodResponse(responseBytes, 500);
                }
                // Priority is automatically restored to NORMAL here via Dispose()
            }
        }

        /// <summary>
        /// Compresses data using GZip compression
        /// </summary>
        private static byte[] CompressGZip(byte[] data)
        {
            using (var outputStream = new MemoryStream())
            {
                using (var gzipStream = new GZipStream(outputStream, CompressionMode.Compress))
                {
                    gzipStream.Write(data, 0, data.Length);
                }
                return outputStream.ToArray();
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
        private async Task<MessageResponse> HandleInputMessage(Message message, object userContext)
        {
            // Elevate process priority to HIGH during execution for optimal performance
            // Priority is automatically restored to NORMAL when execution completes
            using (new ProcessPriorityManager())
            {
                // Create cancellation token with timeout from settings
                var settings = SettingsService.Instance.Settings;
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(settings.ScriptTimeoutSeconds));
                
                try
                {
                    string messageBody = Encoding.UTF8.GetString(message.GetBytes());
                    var request = JsonSerializer.Deserialize<ScriptExecutionRequest>(messageBody);

                    if (request != null && !string.IsNullOrEmpty(request.Script))
                    {
                        var executor = new IoTPowerShellAgent.PowerShell.PowerShellExecutor(_logCallback);
                        // Use async execution with cancellation token to prevent blocking IoT listener
                        await executor.ExecutePowerShellAsync(request.Script, request.IsInlinePowershell, cts.Token).ConfigureAwait(false);
                    }

                    return MessageResponse.Completed;
                }
                catch (OperationCanceledException)
                {
                    OnLog($"Script execution was cancelled or timed out after {settings.ScriptTimeoutSeconds} seconds.", LogOutputType.Warning);
                    return MessageResponse.Abandoned;
                }
                catch (Exception ex)
                {
                    OnLog($"Error processing input message: {ex}", LogOutputType.Error);
                    return MessageResponse.Abandoned;
                }
                // Priority is automatically restored to NORMAL here via Dispose()
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
                    await _moduleClient.SendEventAsync(message).ConfigureAwait(false);
                }
                else if (_deviceClient != null)
                {
                    await _deviceClient.SendEventAsync(message).ConfigureAwait(false);
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
                    await _moduleClient.UpdateReportedPropertiesAsync(reportedProperties).ConfigureAwait(false);
                }
                else if (_deviceClient != null)
                {
                    await _deviceClient.UpdateReportedPropertiesAsync(reportedProperties).ConfigureAwait(false);
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

                // Send as telemetry in background (fire-and-forget with proper error handling)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await SendTelemetryAsync(logTelemetry).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // Log telemetry send failures but don't block
                        // This prevents log telemetry from causing cascading failures
                        try
                        {
                            // Try to log via callback if available, but don't recurse
                            if (_logCallback != null && _logCallback != this)
                            {
                                _logCallback.OnLog($"Failed to send log telemetry: {ex.Message}", LogOutputType.Warning);
                            }
                        }
                        catch
                        {
                            // Silently fail to avoid infinite recursion
                        }
                    }
                }, CancellationToken.None);
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
        /// <summary>
        /// Indicates if the Output field is compressed (gzip + base64).
        /// When true, the Output field contains base64-encoded gzip-compressed data.
        /// Decompress by: base64 decode → gzip decompress → UTF-8 decode
        /// </summary>
        public bool IsCompressed { get; set; } = false;
        /// <summary>
        /// Original size before compression (in bytes). Only set when IsCompressed is true.
        /// </summary>
        public int? OriginalSize { get; set; }
        /// <summary>
        /// Compressed size (in bytes). Only set when IsCompressed is true.
        /// </summary>
        public int? CompressedSize { get; set; }
    }
}

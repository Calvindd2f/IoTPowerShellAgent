using System;
using System.Collections.Generic;
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



    public class IoTHubService : IIoTHubService
    {
        private DeviceClient? _deviceClient;
        private ModuleClient? _moduleClient;
        private readonly SettingsService _settings;
        private readonly ILogCallback? _logCallback;
        private bool _disposed = false;

        public IoTHubService(ILogCallback? logCallback = null)
        {
            _settings = SettingsService.Instance;
            _logCallback = logCallback ?? this;
        }




        public async Task ConnectAsync()
        {
            var transportType = GetTransportType(_settings.Settings.TransportType);

            if (!string.IsNullOrEmpty(_settings.Settings.ModuleId))
            {

                if (!string.IsNullOrEmpty(_settings.Settings.IoTHubConnectionString))
                {
                    _moduleClient = ModuleClient.CreateFromConnectionString(
                        _settings.Settings.IoTHubConnectionString,
                        transportType);
                }
                else
                {

                    _moduleClient = CreateModuleClientFromCredentials(transportType);
                }

                await _moduleClient.OpenAsync().ConfigureAwait(false);
                await _moduleClient.SetMethodHandlerAsync("ExecuteScript", ExecuteScriptMethodHandler, null).ConfigureAwait(false);
                await _moduleClient.SetInputMessageHandlerAsync("scriptInput", HandleInputMessage, null).ConfigureAwait(false);
            }
            else
            {

                if (!string.IsNullOrEmpty(_settings.Settings.IoTHubConnectionString))
                {
                    _deviceClient = DeviceClient.CreateFromConnectionString(
                        _settings.Settings.IoTHubConnectionString,
                        transportType);
                }
                else
                {

                    _deviceClient = CreateDeviceClientFromCredentials(transportType);
                }

                await _deviceClient.OpenAsync().ConfigureAwait(false);
                await _deviceClient.SetMethodHandlerAsync("ExecuteScript", ExecuteScriptMethodHandler, null).ConfigureAwait(false);
            }
        }




        private static TransportType GetTransportType(string transportType)
        {
            return transportType?.ToLowerInvariant() switch
            {
                "mqtt" => TransportType.Mqtt,
                "mqtt_websocket_only" or "mqttwebsocketonly" or "mqtt_ws" => TransportType.Mqtt_WebSocket_Only,
                "amqp_websocket_only" or "amqpwebsocketonly" or "amqp_ws" => TransportType.Amqp_WebSocket_Only,
                "http1" => TransportType.Http1,
                _ => TransportType.Amqp
            };
        }




        private DeviceClient CreateDeviceClientFromCredentials(TransportType transportType)
        {
            if (string.IsNullOrEmpty(_settings.Settings.AzureIotHubHost) ||
                string.IsNullOrEmpty(_settings.Settings.DeviceId) ||
                string.IsNullOrEmpty(_settings.Settings.SharedAccessKey))
            {
                throw new InvalidOperationException(
                    "Either IoTHubConnectionString or AzureIotHubHost, DeviceId, and SharedAccessKey must be provided.");
            }


            string sasToken = SasTokenGenerator.GenerateDeviceSasToken(
                _settings.Settings.AzureIotHubHost,
                _settings.Settings.DeviceId,
                _settings.Settings.SharedAccessKey,
                TimeSpan.FromHours(1));


            var authMethod = new DeviceAuthenticationWithToken(
                _settings.Settings.DeviceId,
                sasToken);


            return DeviceClient.Create(
                _settings.Settings.AzureIotHubHost,
                authMethod,
                transportType);
        }




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


            string resourceUri = $"{_settings.Settings.AzureIotHubHost}/devices/{_settings.Settings.DeviceId}/modules/{_settings.Settings.ModuleId}";
            string sasToken = SasTokenGenerator.GenerateSasToken(
                resourceUri,
                _settings.Settings.SharedAccessKey,
                TimeSpan.FromHours(1));


            var authMethod = new ModuleAuthenticationWithToken(
                _settings.Settings.DeviceId,
                _settings.Settings.ModuleId,
                sasToken);


            return ModuleClient.Create(
                _settings.Settings.AzureIotHubHost,
                authMethod,
                transportType);
        }




        private (bool IsValid, string? ErrorMessage) ValidateScriptExecutionRequest(ScriptExecutionRequest? request)
        {
            if (request == null)
            {
                return (false, "Request payload is null or invalid JSON");
            }

            if (string.IsNullOrWhiteSpace(request.Script))
            {
                return (false, "Script field is required and cannot be empty");
            }


            const int maxScriptLength = 10 * 1024 * 1024;
            if (request.Script.Length > maxScriptLength)
            {
                return (false, $"Script exceeds maximum length of {maxScriptLength / 1024 / 1024}MB");
            }


            if (request.IsBase64Encoded)
            {
                try
                {

                    byte[] bytes = Convert.FromBase64String(request.Script);
                    if (bytes.Length == 0)
                    {
                        return (false, "Base64 encoded script is empty");
                    }
                }
                catch (FormatException)
                {
                    return (false, "Script is marked as base64 encoded but contains invalid base64 data");
                }
            }

            return (true, null);
        }




        private async Task<MethodResponse> ExecuteScriptMethodHandler(MethodRequest methodRequest, object userContext)
        {


            using (new ProcessPriorityManager())
            {

                var settings = SettingsService.Instance.Settings;
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(settings.ScriptTimeoutSeconds));

                try
                {

                    if (string.IsNullOrWhiteSpace(methodRequest.DataAsJson))
                    {
                        var invalidResponse = new ScriptExecutionResponse
                        {
                            Success = false,
                            ErrorMessage = "Request payload is empty or invalid",
                            IsCompressed = false
                        };
                        string invalidResponseJson = JsonSerializer.Serialize(invalidResponse);
                        return new MethodResponse(Encoding.UTF8.GetBytes(invalidResponseJson), 400);
                    }


                    ScriptExecutionRequest? request;
                    try
                    {
                        request = JsonSerializer.Deserialize<ScriptExecutionRequest>(methodRequest.DataAsJson);
                    }
                    catch (JsonException ex)
                    {
                        var invalidResponse = new ScriptExecutionResponse
                        {
                            Success = false,
                            ErrorMessage = $"Invalid JSON payload: {ex.Message}",
                            IsCompressed = false
                        };
                        string invalidResponseJson = JsonSerializer.Serialize(invalidResponse);
                        return new MethodResponse(Encoding.UTF8.GetBytes(invalidResponseJson), 400);
                    }


                    var (isValid, validationError) = ValidateScriptExecutionRequest(request);
                    if (!isValid)
                    {
                        var invalidResponse = new ScriptExecutionResponse
                        {
                            Success = false,
                            ErrorMessage = validationError ?? "Request validation failed",
                            IsCompressed = false
                        };
                        string invalidResponseJson = JsonSerializer.Serialize(invalidResponse);
                        return new MethodResponse(Encoding.UTF8.GetBytes(invalidResponseJson), 400);
                    }


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

                        }
                    }
                    
                    const int compressionThreshold = 1024;

                    if (request.Detached && !string.IsNullOrEmpty(request.ResultEndpoint))
                    {
                        // Detached mode: return immediately and run in background
                        _ = Task.Run(async () =>
                        {
                            var detachedExecutor = new IoTPowerShellAgent.PowerShell.PowerShellExecutor(_logCallback);
                            var detachedResult = await detachedExecutor.ExecutePowerShellAsync(script, request.IsInlinePowershell, cts.Token).ConfigureAwait(false);
                            
                            var detachedResponse = new ScriptExecutionResponse
                            {
                                Success = detachedResult.Success,
                                Output = detachedResult.Output,
                                ErrorMessage = detachedResult.ErrorMessage,
                                ErrorDetails = detachedResult.ErrorDetails,
                                IsCompressed = false
                            };
                            
                            if (!string.IsNullOrEmpty(detachedResult.Output) && detachedResult.Output.Length > compressionThreshold)
                            {
                                byte[] outputBytes = Encoding.UTF8.GetBytes(detachedResult.Output);
                                byte[] compressedBytes = CompressGZip(outputBytes);

                                if (compressedBytes.Length < outputBytes.Length)
                                {
                                    detachedResponse.Output = Convert.ToBase64String(compressedBytes);
                                    detachedResponse.IsCompressed = true;
                                    detachedResponse.OriginalSize = outputBytes.Length;
                                    detachedResponse.CompressedSize = compressedBytes.Length;
                                }
                            }
                            
                            try
                            {
                                string resultJson = JsonSerializer.Serialize(new {
                                    jobId = request.JobId,
                                    deviceId = settings.DeviceId,
                                    success = detachedResponse.Success,
                                    errorMessage = detachedResponse.ErrorMessage,
                                    output = detachedResponse.Output,
                                    isCompressed = detachedResponse.IsCompressed,
                                    originalSize = detachedResponse.OriginalSize,
                                    compressedSize = detachedResponse.CompressedSize
                                });
                                
                                using var http = new System.Net.Http.HttpClient();
                                using var httpRequest = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, request.ResultEndpoint);
                                httpRequest.Content = new System.Net.Http.StringContent(resultJson, Encoding.UTF8, "application/json");
                                if (!string.IsNullOrEmpty(request.JobToken))
                                {
                                    httpRequest.Headers.TryAddWithoutValidation("x-job-token", request.JobToken);
                                }
                                
                                await http.SendAsync(httpRequest).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                OnLog($"Failed to post detached job result: {ex.Message}", LogOutputType.Error);
                            }
                        }, CancellationToken.None);

                        var acceptedResponse = new { accepted = true, message = "Job started in background" };
                        return new MethodResponse(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(acceptedResponse)), 202);
                    }

                    var executor = new IoTPowerShellAgent.PowerShell.PowerShellExecutor(_logCallback);

                    var result = await executor.ExecutePowerShellAsync(script, request.IsInlinePowershell, cts.Token).ConfigureAwait(false);




                    var response = new ScriptExecutionResponse
                    {
                        Success = result.Success,
                        Output = result.Output,
                        ErrorMessage = result.ErrorMessage,
                        ErrorDetails = result.ErrorDetails,
                        IsCompressed = false
                    };


                    if (!string.IsNullOrEmpty(result.Output) && result.Output.Length > compressionThreshold)
                    {
                        byte[] outputBytes = Encoding.UTF8.GetBytes(result.Output);
                        byte[] compressedBytes = CompressGZip(outputBytes);


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


                    return new MethodResponse(responseBytes, 408);
                }
                catch (Exception ex)
                {
                    var errorResponse = new ScriptExecutionResponse
                    {
                        Success = false,
                        ErrorMessage = ex.ToString(),
                        IsCompressed = false
                    };


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


                    return new MethodResponse(responseBytes, 500);
                }

            }
        }




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




        private async Task<MessageResponse> HandleInputMessage(Message message, object userContext)
        {


            using (new ProcessPriorityManager())
            {

                var settings = SettingsService.Instance.Settings;
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(settings.ScriptTimeoutSeconds));

                try
                {
                    string messageBody = Encoding.UTF8.GetString(message.GetBytes());


                    if (string.IsNullOrWhiteSpace(messageBody))
                    {
                        OnLog("Input message payload is empty or invalid", LogOutputType.Error);
                        return MessageResponse.Abandoned;
                    }

                    ScriptExecutionRequest? request;
                    try
                    {
                        request = JsonSerializer.Deserialize<ScriptExecutionRequest>(messageBody);
                    }
                    catch (JsonException ex)
                    {
                        OnLog($"Invalid JSON payload in input message: {ex.Message}", LogOutputType.Error);
                        return MessageResponse.Abandoned;
                    }


                    var (isValid, validationError) = ValidateScriptExecutionRequest(request);
                    if (!isValid)
                    {
                        OnLog($"Input message validation failed: {validationError}", LogOutputType.Error);
                        return MessageResponse.Abandoned;
                    }

                    if (request != null && !string.IsNullOrEmpty(request.Script))
                    {
                        var executor = new IoTPowerShellAgent.PowerShell.PowerShellExecutor(_logCallback);

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

            }
        }




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


                _ = Task.Run(async () =>
                {
                    try
                    {
                        await SendTelemetryAsync(logTelemetry).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {


                        try
                        {

                            if (_logCallback != null && _logCallback != this)
                            {
                                _logCallback.OnLog($"Failed to send log telemetry: {ex.Message}", LogOutputType.Warning);
                            }
                        }
                        catch
                        {

                        }
                    }
                }, CancellationToken.None);
            }
            catch
            {

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




    public class ScriptExecutionRequest
    {
        public string Script { get; set; } = string.Empty;
        public bool IsInlinePowershell { get; set; } = false;
        public bool IsBase64Encoded { get; set; } = false;

        [System.Text.Json.Serialization.JsonPropertyName("JobId")]
        public string? JobId { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("ResultEndpoint")]
        public string? ResultEndpoint { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("JobToken")]
        public string? JobToken { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("TimeoutSeconds")]
        public int? TimeoutSeconds { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("Detached")]
        public bool Detached { get; set; } = false;
    }




    public class ScriptExecutionResponse
    {
        public bool Success { get; set; }
        public string Output { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;



        public List<PowerShellErrorDetails>? ErrorDetails { get; set; }





        public bool IsCompressed { get; set; } = false;



        public int? OriginalSize { get; set; }



        public int? CompressedSize { get; set; }
    }
}

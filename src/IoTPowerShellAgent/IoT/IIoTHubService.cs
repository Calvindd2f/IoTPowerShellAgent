using System;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Shared;
using IoTPowerShellAgent.Core;
using IoTPowerShellAgent.PowerShell;

namespace IoTPowerShellAgent.IoT
{
    /// <summary>
    /// Interface for Azure IoT Hub integration
    /// Enables unit testing with mocks
    /// </summary>
    public interface IIoTHubService : IDisposable, ILogCallback
    {
        /// <summary>
        /// Connects to Azure IoT Hub
        /// </summary>
        Task ConnectAsync();

        /// <summary>
        /// Sends telemetry data to IoT Hub
        /// </summary>
        Task SendTelemetryAsync(object telemetryData);

        /// <summary>
        /// Updates device twin reported properties
        /// </summary>
        Task UpdateTwinAsync(TwinCollection reportedProperties);
    }
}


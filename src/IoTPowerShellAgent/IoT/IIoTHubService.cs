using System;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Shared;
using IoTPowerShellAgent.Core;
using IoTPowerShellAgent.PowerShell;

namespace IoTPowerShellAgent.IoT
{




    public interface IIoTHubService : IDisposable, ILogCallback
    {



        Task ConnectAsync();




        Task SendTelemetryAsync(object telemetryData);




        Task UpdateTwinAsync(TwinCollection reportedProperties);
    }
}


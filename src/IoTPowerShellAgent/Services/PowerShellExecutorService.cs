using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Shared;
using IoTPowerShellAgent.IoT;
using IoTPowerShellAgent.Utilities;

namespace IoTPowerShellAgent.Services
{
    /// <summary>
    /// Windows Service for PowerShell execution with Azure IoT Hub integration
    /// </summary>
    public partial class PowerShellExecutorService : ServiceBase
    {
        private IoTHubService? _iotHubService;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _serviceTask;

        public PowerShellExecutorService()
        {
            InitializeComponent();
            ServiceName = "IoTPowerShellAgent";
        }

        protected override void OnStart(string[] args)
        {
            // Optimize process priority for service performance
            try
            {
                ProcessUtil.SetProcessPriority(WindowsApiInterop.NORMAL_PRIORITY_CLASS);
            }
            catch
            {
                // Ignore if setting priority fails
            }

            _cancellationTokenSource = new CancellationTokenSource();

            _serviceTask = Task.Run(async () =>
            {
                try
                {
                    // Set thread priority for better responsiveness
                    ProcessUtil.SetThreadPriority(WindowsApiInterop.THREAD_PRIORITY_NORMAL);

                    _iotHubService = new IoTHubService();
                    await _iotHubService.ConnectAsync();

                    // Report service status to IoT Hub
                    await ReportServiceStatusAsync("Running");

                    // Keep service running
                    while (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        await Task.Delay(1000, _cancellationTokenSource.Token);
                    }
                }
                catch (Exception ex)
                {
                    // Log error but don't crash service
                    System.Diagnostics.EventLog.WriteEntry(ServiceName,
                        $"Service error: {ex}",
                        System.Diagnostics.EventLogEntryType.Error);
                }
            });
        }

        protected override void OnStop()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _serviceTask?.Wait(TimeSpan.FromSeconds(30));

                if (_iotHubService != null)
                {
                    ReportServiceStatusAsync("Stopped").Wait(TimeSpan.FromSeconds(5));
                    _iotHubService.Dispose();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.EventLog.WriteEntry(ServiceName,
                    $"Error stopping service: {ex}",
                    System.Diagnostics.EventLogEntryType.Error);
            }
        }

        private async Task ReportServiceStatusAsync(string status)
        {
            try
            {
                if (_iotHubService != null)
                {
                    // Use detailed memory info via P/Invoke for better performance
                    var (workingSetMB, privateMB, peakMB) = ProcessUtil.GetDetailedMemoryInfo();
                    var cpuUsage = ProcessUtil.GetCpuUsage();

                    var properties = new TwinCollection
                    {
                        ["serviceStatus"] = status,
                        ["lastUpdate"] = DateTime.UtcNow,
                        ["memoryUsageMB"] = workingSetMB,
                        ["privateMemoryMB"] = privateMB,
                        ["peakMemoryMB"] = peakMB,
                        ["cpuUsagePercent"] = Math.Round(cpuUsage, 2)
                    };

                    await _iotHubService.UpdateTwinAsync(properties);
                }
            }
            catch
            {
                // Silently fail - don't crash service if reporting fails
            }
        }

        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer? components = null;

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

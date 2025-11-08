using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Shared;
using IoTPowerShellAgent.Core;
using IoTPowerShellAgent.IoT;
using IoTPowerShellAgent.Utilities;
using IoTPowerShellAgent.PowerShell;

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
        private Task? _autoUpdaterTask;
        private EnvironmentMetricsCollector? _metricsCollector;
        private AutoUpdater? _autoUpdater;
        private EventLogCallback? _eventLogCallback;

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

                    // Initialize event log callback first
                    _eventLogCallback = new EventLogCallback(ServiceName);

                    // Initialize IoT Hub service with event log callback
                    // EventLogCallback will write to event log, then forward to IoT Hub for telemetry
                    _iotHubService = new IoTHubService(_eventLogCallback);
                    await _iotHubService.ConnectAsync().ConfigureAwait(false);

                    // Initialize metrics collector
                    _metricsCollector = new EnvironmentMetricsCollector(_iotHubService);

                    // Collect and log environment metrics
                    await _metricsCollector.LogAllMetricsAsync(_cancellationTokenSource.Token).ConfigureAwait(false);

                    // Initialize and start auto-updater if enabled
                    var settings = SettingsService.Instance.Settings;
                    if (settings.EnableAutoUpdates)
                    {
                        var updateInterval = TimeSpan.FromHours(settings.AutoUpdateIntervalHours);
                        _autoUpdater = new AutoUpdater(
                            _eventLogCallback, // Use event log callback for auto-updater logs
                            settings.GitHubReleaseUrl,
                            enabled: true,
                            updateInterval: updateInterval
                        );

                        // Start auto-updater in background
                        _autoUpdaterTask = Task.Run(() => _autoUpdater.RunAutoUpdaterAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
                    }

                    // Report service status to IoT Hub
                    await ReportServiceStatusAsync("Running").ConfigureAwait(false);

                    // Keep service running
                    while (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        await Task.Delay(1000, _cancellationTokenSource.Token).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    // Log error to event log
                    _eventLogCallback?.WriteError($"Service error: {ex}");
                }
            });
        }

        protected override void OnStop()
        {
            try
            {
                _cancellationTokenSource?.Cancel();

                // Wait for both service task and auto-updater task
                _serviceTask?.Wait(TimeSpan.FromSeconds(30));
                _autoUpdaterTask?.Wait(TimeSpan.FromSeconds(10));

                if (_iotHubService != null)
                {
                    ReportServiceStatusAsync("Stopped").Wait(TimeSpan.FromSeconds(5));
                    _iotHubService.Dispose();
                }

                _autoUpdater?.Dispose();
                _eventLogCallback?.Dispose();
            }
            catch (Exception ex)
            {
                // Log error to event log
                _eventLogCallback?.WriteError($"Error stopping service: {ex}");
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
                        ["cpuUsagePercent"] = Math.Round(cpuUsage, 2),
                        ["version"] = Core.VersionInfo.Version
                    };

                    // Add environment metrics if collector is available
                    if (_metricsCollector != null)
                    {
                        try
                        {
                            var metrics = await _metricsCollector.CollectAllMetricsAsync(_cancellationTokenSource?.Token ?? CancellationToken.None).ConfigureAwait(false);

                            // Add metrics to twin properties
                            foreach (var metric in metrics)
                            {
                                if (metric.Key != "collectedAt" && metric.Value != null)
                                {
                                    properties[metric.Key] = metric.Value;
                                }
                            }

                            if (metrics.ContainsKey("collectedAt") && metrics["collectedAt"] is DateTime collectedAt)
                            {
                                properties["environmentMetricsCollectedAt"] = collectedAt;
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log but don't fail - metrics collection is optional
                            _eventLogCallback?.WriteWarning($"Failed to collect environment metrics: {ex.Message}");
                        }
                    }

                    await _iotHubService.UpdateTwinAsync(properties).ConfigureAwait(false);
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

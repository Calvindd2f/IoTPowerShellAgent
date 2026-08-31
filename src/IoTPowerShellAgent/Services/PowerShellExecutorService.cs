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




    public partial class PowerShellExecutorService : ServiceBase
    {
        private IIoTHubService? _iotHubService;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _serviceTask;
        private Task? _autoUpdaterTask;
        private EnvironmentMetricsCollector? _metricsCollector;
        private AutoUpdater? _autoUpdater;
        private EventLogCallback? _eventLogCallback;




        public PowerShellExecutorService(IIoTHubService? iotHubService = null)
        {
            InitializeComponent();
            ServiceName = "IoTPowerShellAgent";
            _iotHubService = iotHubService;
        }

        protected override void OnStart(string[] args)
        {

            try
            {
                ProcessUtil.SetProcessPriority(WindowsApiInterop.NORMAL_PRIORITY_CLASS);
            }
            catch
            {

            }

            _cancellationTokenSource = new CancellationTokenSource();

            _serviceTask = Task.Run(async () =>
            {
                try
                {

                    ProcessUtil.SetThreadPriority(WindowsApiInterop.THREAD_PRIORITY_NORMAL);


                    _eventLogCallback = new EventLogCallback(ServiceName);



                    if (_iotHubService == null)
                    {
                        _iotHubService = new IoTHubService(_eventLogCallback);
                    }
                    await _iotHubService.ConnectAsync().ConfigureAwait(false);


                    _metricsCollector = new EnvironmentMetricsCollector(_iotHubService);


                    await _metricsCollector.LogAllMetricsAsync(_cancellationTokenSource.Token).ConfigureAwait(false);


                    var settings = SettingsService.Instance.Settings;
                    if (settings.EnableAutoUpdates)
                    {
                        var updateInterval = TimeSpan.FromHours(settings.AutoUpdateIntervalHours);
                        _autoUpdater = new AutoUpdater(
                            _eventLogCallback,
                            settings.GitHubReleaseUrl,
                            enabled: true,
                            updateInterval: updateInterval
                        );


                        _autoUpdaterTask = Task.Run(() => _autoUpdater.RunAutoUpdaterAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
                    }


                    await ReportServiceStatusAsync("Running").ConfigureAwait(false);



                    while (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        await Task.Delay(1000, _cancellationTokenSource.Token).ConfigureAwait(false);



                        await Task.Yield();
                    }
                }
                catch (OperationCanceledException)
                {

                    _eventLogCallback?.OnLog("Service cancellation requested", LogOutputType.Information);
                }
                catch (AggregateException aggEx)
                {

                    foreach (var innerEx in aggEx.InnerExceptions)
                    {
                        _eventLogCallback?.WriteError($"Service error: {innerEx}");
                    }
                }
                catch (Exception ex)
                {

                    _eventLogCallback?.WriteError($"Service error: {ex}");


                    try
                    {
                        await ReportServiceStatusAsync("Error").ConfigureAwait(false);
                    }
                    catch
                    {

                    }
                }
            });
        }

        protected override void OnStop()
        {
            try
            {
                _cancellationTokenSource?.Cancel();



                try
                {
                    _serviceTask?.GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {

                }
                catch (AggregateException aggEx)
                {

                    foreach (var innerEx in aggEx.InnerExceptions)
                    {
                        _eventLogCallback?.WriteError($"Service task error: {innerEx}");
                    }
                }

                try
                {
                    _autoUpdaterTask?.GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {

                }
                catch (AggregateException aggEx)
                {
                    foreach (var innerEx in aggEx.InnerExceptions)
                    {
                        _eventLogCallback?.WriteError($"Auto-updater task error: {innerEx}");
                    }
                }


                if (_iotHubService != null)
                {
                    try
                    {
                        ReportServiceStatusAsync("Stopped").GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        _eventLogCallback?.WriteWarning($"Failed to report stopped status: {ex.Message}");
                    }

                    _iotHubService.Dispose();
                }

                _autoUpdater?.Dispose();
                _eventLogCallback?.Dispose();
                _cancellationTokenSource?.Dispose();
            }
            catch (Exception ex)
            {

                _eventLogCallback?.WriteError($"Error stopping service: {ex}");
            }
        }

        private async Task ReportServiceStatusAsync(string status)
        {
            try
            {
                if (_iotHubService != null)
                {

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


                    if (_metricsCollector != null)
                    {
                        try
                        {
                            var metrics = await _metricsCollector.CollectAllMetricsAsync(_cancellationTokenSource?.Token ?? CancellationToken.None).ConfigureAwait(false);


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

                            _eventLogCallback?.WriteWarning($"Failed to collect environment metrics: {ex.Message}");
                        }
                    }

                    await _iotHubService.UpdateTwinAsync(properties).ConfigureAwait(false);
                }
            }
            catch
            {

            }
        }




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

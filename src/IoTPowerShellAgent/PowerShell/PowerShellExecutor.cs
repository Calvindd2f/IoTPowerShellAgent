using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Text;
using System.Globalization;
using System.Threading.Tasks;
using IoTPowerShellAgent.Core;

namespace IoTPowerShellAgent.PowerShell
{
    /// <summary>
    /// Executes PowerShell scripts and handles output streaming
    /// </summary>
    public class PowerShellExecutor : IDisposable
    {
        private readonly ILogCallback? _logCallback;
        public int verboseLinesProcessed = 0;
        public int warningLinesProcessed = 0;
        public int errorLinesProcessed = 0;
        public int informationLinesProcessed = 0;
        public int activityLogCounter = 0;
        public int activityLogThreshold = 1000;

        public PowerShellExecutor(ILogCallback? logCallback = null)
        {
            this.activityLogCounter = 0;
            this._logCallback = logCallback;
            var settings = SettingsService.Instance.Settings;
            this.activityLogThreshold = settings.ActivityLogThreshold;
        }

        public void SendLog(string logOutput, LogOutputType logtype)
        {
            if (string.IsNullOrEmpty(logOutput))
                return;

            if (this.activityLogCounter > this.activityLogThreshold)
            {
                throw new Exception("Activity Log threshold exceeded.");
            }
            ++this.activityLogCounter;

            _logCallback?.OnLog(logOutput, logtype);
        }

        public void Debug_DataAdded(object sender, DataAddedEventArgs e)
        {
            if (sender is PSDataCollection<DebugRecord> debugCollection)
            {
                string logOutput = "";
                for (int i = this.informationLinesProcessed; i < debugCollection.Count; i++)
                {
                    DebugRecord debugRecord = debugCollection[i];
                    logOutput += debugRecord.Message + Environment.NewLine;
                    ++this.informationLinesProcessed;
                }
                if (!string.IsNullOrEmpty(logOutput))
                {
                    this.SendLog(logOutput.Trim(), LogOutputType.Debug);
                }
            }
        }

        public void Progress_DataAdded(object sender, DataAddedEventArgs e)
        {
            if (sender is PSDataCollection<ProgressRecord> progressCollection)
            {
                string logOutput = "";
                for (int i = 0; i < progressCollection.Count; i++)
                {
                    ProgressRecord progressRecord = progressCollection[i];
                    logOutput += $"{progressRecord.Activity}: {progressRecord.StatusDescription} ({progressRecord.PercentComplete}%)" + Environment.NewLine;
                }
                if (!string.IsNullOrEmpty(logOutput))
                {
                    this.SendLog(logOutput.Trim(), LogOutputType.Progress);
                }
            }
        }

        public void Error_DataAdded(object sender, DataAddedEventArgs e)
        {
            if (sender is PSDataCollection<ErrorRecord> errorCollection)
            {
                string logOutput = "";
                for (int i = this.errorLinesProcessed; i < errorCollection.Count; i++)
                {
                    ErrorRecord errorRecord = errorCollection[i];
                    logOutput += errorRecord.Exception?.Message ?? errorRecord.ToString();
                    logOutput += Environment.NewLine;
                    ++this.errorLinesProcessed;
                }
                if (!string.IsNullOrEmpty(logOutput))
                {
                    this.SendLog(logOutput.Trim(), LogOutputType.Error);
                }
            }
        }

        public void Verbose_DataAdded(object sender, DataAddedEventArgs e)
        {
            if (sender is PSDataCollection<VerboseRecord> verboseCollection)
            {
                string logOutput = "";
                for (int i = this.verboseLinesProcessed; i < verboseCollection.Count; i++)
                {
                    VerboseRecord verboseRecord = verboseCollection[i];
                    logOutput += verboseRecord.Message + Environment.NewLine;
                    ++this.verboseLinesProcessed;
                }
                if (!string.IsNullOrEmpty(logOutput))
                {
                    this.SendLog(logOutput.Trim(), LogOutputType.Verbose);
                }
            }
        }

        public void Warning_DataAdded(object sender, DataAddedEventArgs e)
        {
            if (sender is PSDataCollection<WarningRecord> warningCollection)
            {
                string logOutput = "";
                for (int i = this.warningLinesProcessed; i < warningCollection.Count; i++)
                {
                    WarningRecord warningRecord = warningCollection[i];
                    logOutput += warningRecord.Message + Environment.NewLine;
                    ++this.warningLinesProcessed;
                }
                if (!string.IsNullOrEmpty(logOutput))
                {
                    this.SendLog(logOutput.Trim(), LogOutputType.Warning);
                }
            }
        }

        public void Information_DataAdded(object sender, DataAddedEventArgs e)
        {
            if (sender is PSDataCollection<InformationRecord> infoCollection)
            {
                string logOutput = "";
                for (int i = this.informationLinesProcessed; i < infoCollection.Count; i++)
                {
                    InformationRecord infoRecord = infoCollection[i];
                    logOutput += infoRecord.MessageData?.ToString() ?? string.Empty;
                    logOutput += Environment.NewLine;
                    ++this.informationLinesProcessed;
                }
                if (!string.IsNullOrEmpty(logOutput))
                {
                    this.SendLog(logOutput.Trim(), LogOutputType.Information);
                }
            }
        }

        public void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e?.Data != null && e.Data.Length > 0)
            {
                this.SendLog(e.Data, LogOutputType.Information);
            }
        }

        public void Host_OnInformation(string information)
        {
            string logOutput = information?.Trim() ?? string.Empty;
            if (logOutput.Length > 0)
            {
                this.SendLog(logOutput, LogOutputType.Information);
            }
        }

        public void HandleInformation(string logOutput)
        {
            if (string.IsNullOrEmpty(logOutput))
                return;
            this.SendLog(logOutput, LogOutputType.Information);
        }

        public void BindEvents(System.Management.Automation.PowerShell ps, DefaultHost host)
        {
            ps.Streams.Debug.DataAdded += new EventHandler<DataAddedEventArgs>(this.Debug_DataAdded);
            ps.Streams.Error.DataAdded += new EventHandler<DataAddedEventArgs>(this.Error_DataAdded);
            ps.Streams.Progress.DataAdded += new EventHandler<DataAddedEventArgs>(this.Progress_DataAdded);

            // Try to bind Information stream if available (PowerShell 5.0+)
            try
            {
                PropertyInfo property = ps.Streams.GetType().GetProperty("Information");
                if (property != null)
                {
                    object? target = property.GetValue(ps.Streams);
                    if (target != null)
                    {
                        EventInfo? eventInfo = target.GetType().GetEvent("DataAdded");
                        if (eventInfo != null)
                        {
                            MethodInfo method = this.GetType().GetMethod("Information_DataAdded", BindingFlags.Instance | BindingFlags.Public);
                            if (method != null)
                            {
                                Delegate? handler = Delegate.CreateDelegate(eventInfo.EventHandlerType, this, method);
                                if (handler != null)
                                {
                                    eventInfo.AddEventHandler(target, handler);
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fallback to host information if Information stream is not available
                host.OnInformation += new DefaultHost.InformationDelegate(this.Host_OnInformation);
            }

            ps.Streams.Verbose.DataAdded += new EventHandler<DataAddedEventArgs>(this.Verbose_DataAdded);
            ps.Streams.Warning.DataAdded += new EventHandler<DataAddedEventArgs>(this.Warning_DataAdded);
        }

        public PowerShellExecutionResult ExecutePowerShell(string script, bool isInlinePowershell)
        {
            PowerShellExecutionResult result = new PowerShellExecutionResult();
            Runspace? runspace = null;
            System.Management.Automation.PowerShell? powerShell = null;
            PSObject? psobject2 = null;

            try
            {
                if (string.IsNullOrWhiteSpace(script))
                {
                    throw new ArgumentException("Script cannot be null or empty", nameof(script));
                }

                            SettingsService settingsService = SettingsService.Instance;
            bool isActivityNode = settingsService.GetIsActivityNode();

                        DefaultHost defaultHost = new DefaultHost(CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
                // In PowerShell 7, CreateDefault() may try to load snap-ins which can fail
                // Use CreateDefault2() which creates a minimal session state without snap-ins
                InitialSessionState initialSessionState = InitialSessionState.CreateDefault2();

                runspace = RunspaceFactory.CreateRunspace(defaultHost, initialSessionState);
                runspace.Open();
                
                powerShell = System.Management.Automation.PowerShell.Create();
                powerShell.Runspace = runspace;
                this.BindEvents(powerShell, defaultHost);

                // Pre-load core PowerShell modules to avoid PSSnapIn compatibility issues
                // We need to load modules before the script runs to catch any loading errors early
                try
                {
                    // Use using statement to ensure PowerShell instance is always disposed
                    // even if SendLog() throws an exception during error processing
                    using (var preloadPs = System.Management.Automation.PowerShell.Create())
                    {
                        preloadPs.Runspace = runspace;

                        // Create a script that pre-loads commonly used modules
                        // This uses direct module path access to bypass auto-loading issues
                        preloadPs.AddScript(@"
                            $ErrorActionPreference = 'SilentlyContinue';
                            # Try to pre-load core modules using their full paths
                            # This bypasses the auto-import mechanism that can trigger PSSnapIn errors
                            $psHome = $PSHOME;
                            $managementPath = Join-Path $psHome 'Modules\Microsoft.PowerShell.Management\Microsoft.PowerShell.Management.psd1';
                            $utilityPath = Join-Path $psHome 'Modules\Microsoft.PowerShell.Utility\Microsoft.PowerShell.Utility.psd1';
                            
                            # Load modules if they exist and aren't already loaded
                            if (Test-Path $managementPath) {
                                if (-not (Get-Module -Name Microsoft.PowerShell.Management)) {
                                    try {
                                        Import-Module $managementPath -Force -SkipEditionCheck -ErrorAction Stop;
                                    } catch {
                                        # If import fails, try loading the .psm1 file directly
                                        $psm1Path = Join-Path (Split-Path $managementPath -Parent) 'Microsoft.PowerShell.Management.psm1';
                                        if (Test-Path $psm1Path) {
                                            Import-Module $psm1Path -Force -SkipEditionCheck -ErrorAction SilentlyContinue;
                                        }
                                    }
                                }
                            }
                            if (Test-Path $utilityPath) {
                                if (-not (Get-Module -Name Microsoft.PowerShell.Utility)) {
                                    try {
                                        Import-Module $utilityPath -Force -SkipEditionCheck -ErrorAction Stop;
                                    } catch {
                                        # If import fails, try loading the .psm1 file directly
                                        $psm1Path = Join-Path (Split-Path $utilityPath -Parent) 'Microsoft.PowerShell.Utility.psm1';
                                        if (Test-Path $psm1Path) {
                                            Import-Module $psm1Path -Force -SkipEditionCheck -ErrorAction SilentlyContinue;
                                        }
                                    }
                                }
                            }
                        ", false);

                        preloadPs.Invoke();
                        var preloadErrors = preloadPs.Streams.Error;

                        // Log any errors but don't fail - modules might still auto-load on demand
                        // Note: SendLog() could throw, but using statement ensures preloadPs is disposed
                        foreach (var error in preloadErrors)
                        {
                            if (error.Exception != null)
                            {
                                var errorMsg = error.Exception.Message;
                                // Suppress PSSnapIn errors as they're expected in some configurations
                                if (!errorMsg.Contains("PSSnapIn") && !errorMsg.Contains("Could not load type"))
                                {
                                    this.SendLog($"Module preload warning: {errorMsg}", LogOutputType.Warning);
                                }
                            }
                        }
                    } // preloadPs.Dispose() called here automatically
                }
                catch (Exception ex)
                {
                    // Non-fatal - continue execution as modules may still work
                    this.SendLog($"Module preload exception (non-fatal): {ex.Message}", LogOutputType.Warning);
                }
                
                powerShell.AddScript(script, false);

                PSInvocationSettings psinvocationSettings = new PSInvocationSettings();
                psinvocationSettings.Host = defaultHost;

                Collection<PSObject> output = powerShell.Invoke();

                // Check for errors - collect all error messages
                if (powerShell.Streams.Error.Count > 0)
                {
                    StringBuilder errorBuilder = new StringBuilder();
                    foreach (ErrorRecord error in powerShell.Streams.Error)
                    {
                        string errorMsg = error.Exception?.Message ?? error.ToString();
                        // Check if this is a command not found error (module not loaded)
                        if (errorMsg.Contains("is not recognized") ||
                            errorMsg.Contains("was not found") ||
                            errorMsg.Contains("could not be loaded"))
                        {
                            errorBuilder.AppendLine($"Command Error: {errorMsg}");
                        }
                        else
                        {
                            errorBuilder.AppendLine(errorMsg);
                        }
                    }
                    result.ErrorMessage = errorBuilder.ToString();
                    result.Success = false;
                }

                // Result validation and processing
                // Note: Some commands like Format-Table output to host, not pipeline
                // So empty output is not always an error
                if (output.Count == 0 && (!isActivityNode || !isInlinePowershell))
                {
                    // Only fail if there are actual errors
                    // Empty output is acceptable for commands that output to host
                    if (!string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        // There are errors, so the execution failed
                        result.Success = false;
                    }
                    else
                    {
                        // No errors and no output - this is acceptable for host-output commands
                        result.Success = true;
                        result.Output = string.Empty;
                    }
                }

                if (output.Count > 1)
                {
                    throw new Exception("Activity returned more than one result. See below for details");
                }

                if (output.Count > 0)
                {
                    psobject2 = output[0];
                    result.RawOutput = psobject2;
                    result.Output = psobject2.ToString() ?? string.Empty;
                    result.Success = true;
                }
                else if (string.IsNullOrEmpty(result.ErrorMessage))
                {
                    result.Success = true;
                    result.Output = string.Empty;
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Exception = ex;
                result.ErrorMessage = ex.ToString();
                this.SendLog(ex.ToString(), LogOutputType.Error);
            }
            finally
            {
                if (runspace != null)
                {
                    runspace.Close();
                    runspace.Dispose();
                }
                if (powerShell != null)
                {
                    powerShell.Dispose();
                }
                if (psobject2 is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            return result;
        }

        public void Dispose()
        {
            // Cleanup if needed
        }
    }

    /// <summary>
    /// Interface for logging callbacks
    /// </summary>
    public interface ILogCallback
    {
        void OnLog(string message, LogOutputType logType);
    }
}
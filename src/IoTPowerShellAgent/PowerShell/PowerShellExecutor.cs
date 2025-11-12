using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Text;
using System.Globalization;
using System.Threading.Tasks;
using System.Threading;
using IoTPowerShellAgent.Core;
using IoTPowerShellAgent.Utilities;
using System.Linq;

namespace IoTPowerShellAgent.PowerShell
{
    /// <summary>
    /// Executes PowerShell scripts and handles output streaming
    /// </summary>
    public class PowerShellExecutor : IDisposable
    {
        private readonly ILogCallback? _logCallback;
        private int _verboseLinesProcessed = 0;
        private int _warningLinesProcessed = 0;
        private int _errorLinesProcessed = 0;
        private int _informationLinesProcessed = 0;
        private int _debugLinesProcessed = 0;
        private int _activityLogCounter = 0;
        private readonly int _activityLogThreshold;
        private static SemaphoreSlim? _executionSemaphore;
        private static readonly object _semaphoreLock = new object();

        public PowerShellExecutor(ILogCallback? logCallback = null)
        {
            _activityLogCounter = 0;
            _logCallback = logCallback;
            var settings = SettingsService.Instance.Settings;
            _activityLogThreshold = settings.ActivityLogThreshold;

            // Initialize semaphore if not already initialized
            InitializeSemaphore(settings.MaxConcurrentRunspaces);
        }

        /// <summary>
        /// Initializes the execution semaphore for throttling concurrent runspaces
        /// </summary>
        private static void InitializeSemaphore(int maxConcurrent)
        {
            if (_executionSemaphore == null)
            {
                lock (_semaphoreLock)
                {
                    if (_executionSemaphore == null)
                    {
                        // Ensure minimum of 1 and reasonable maximum
                        int semaphoreCount = Math.Max(1, Math.Min(maxConcurrent, 10));
                        _executionSemaphore = new SemaphoreSlim(semaphoreCount, semaphoreCount);
                    }
                }
            }
        }

        public void SendLog(string logOutput, LogOutputType logtype)
        {
            if (string.IsNullOrEmpty(logOutput))
                return;

            if (_activityLogCounter > _activityLogThreshold)
            {
                throw new Exception("Activity Log threshold exceeded.");
            }
            ++_activityLogCounter;

            _logCallback?.OnLog(logOutput, logtype);
        }

        public void Debug_DataAdded(object sender, DataAddedEventArgs e)
        {
            if (sender is PSDataCollection<DebugRecord> debugCollection)
            {
                var logBuilder = new StringBuilder();
                for (int i = _debugLinesProcessed; i < debugCollection.Count; i++)
                {
                    DebugRecord debugRecord = debugCollection[i];
                    logBuilder.AppendLine(debugRecord.Message);
                    ++_debugLinesProcessed;
                }
                if (logBuilder.Length > 0)
                {
                    SendLog(logBuilder.ToString().Trim(), LogOutputType.Debug);
                }
            }
        }

        public void Progress_DataAdded(object sender, DataAddedEventArgs e)
        {
            if (sender is PSDataCollection<ProgressRecord> progressCollection)
            {
                var logBuilder = new StringBuilder();
                for (int i = 0; i < progressCollection.Count; i++)
                {
                    ProgressRecord progressRecord = progressCollection[i];
                    logBuilder.AppendLine($"{progressRecord.Activity}: {progressRecord.StatusDescription} ({progressRecord.PercentComplete}%)");
                }
                if (logBuilder.Length > 0)
                {
                    SendLog(logBuilder.ToString().Trim(), LogOutputType.Progress);
                }
            }
        }

        public void Error_DataAdded(object sender, DataAddedEventArgs e)
        {
            if (sender is PSDataCollection<ErrorRecord> errorCollection)
            {
                var logBuilder = new StringBuilder();
                for (int i = _errorLinesProcessed; i < errorCollection.Count; i++)
                {
                    ErrorRecord errorRecord = errorCollection[i];
                    logBuilder.AppendLine(errorRecord.Exception?.Message ?? errorRecord.ToString());
                    ++_errorLinesProcessed;
                }
                if (logBuilder.Length > 0)
                {
                    SendLog(logBuilder.ToString().Trim(), LogOutputType.Error);
                }
            }
        }

        public void Verbose_DataAdded(object sender, DataAddedEventArgs e)
        {
            if (sender is PSDataCollection<VerboseRecord> verboseCollection)
            {
                var logBuilder = new StringBuilder();
                for (int i = _verboseLinesProcessed; i < verboseCollection.Count; i++)
                {
                    VerboseRecord verboseRecord = verboseCollection[i];
                    logBuilder.AppendLine(verboseRecord.Message);
                    ++_verboseLinesProcessed;
                }
                if (logBuilder.Length > 0)
                {
                    SendLog(logBuilder.ToString().Trim(), LogOutputType.Verbose);
                }
            }
        }

        public void Warning_DataAdded(object sender, DataAddedEventArgs e)
        {
            if (sender is PSDataCollection<WarningRecord> warningCollection)
            {
                var logBuilder = new StringBuilder();
                for (int i = _warningLinesProcessed; i < warningCollection.Count; i++)
                {
                    WarningRecord warningRecord = warningCollection[i];
                    logBuilder.AppendLine(warningRecord.Message);
                    ++_warningLinesProcessed;
                }
                if (logBuilder.Length > 0)
                {
                    SendLog(logBuilder.ToString().Trim(), LogOutputType.Warning);
                }
            }
        }

        public void Information_DataAdded(object sender, DataAddedEventArgs e)
        {
            if (sender is PSDataCollection<InformationRecord> infoCollection)
            {
                var logBuilder = new StringBuilder();
                for (int i = _informationLinesProcessed; i < infoCollection.Count; i++)
                {
                    InformationRecord infoRecord = infoCollection[i];
                    logBuilder.AppendLine(infoRecord.MessageData?.ToString() ?? string.Empty);
                    ++_informationLinesProcessed;
                }
                if (logBuilder.Length > 0)
                {
                    SendLog(logBuilder.ToString().Trim(), LogOutputType.Information);
                }
            }
        }

        public void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e?.Data != null && e.Data.Length > 0)
            {
                SendLog(e.Data, LogOutputType.Information);
            }
        }

        public void Host_OnInformation(string information)
        {
            string logOutput = information?.Trim() ?? string.Empty;
            if (logOutput.Length > 0)
            {
                SendLog(logOutput, LogOutputType.Information);
            }
        }

        public void HandleInformation(string logOutput)
        {
            if (string.IsNullOrEmpty(logOutput))
                return;
            SendLog(logOutput, LogOutputType.Information);
        }

        public void BindEvents(System.Management.Automation.PowerShell ps, DefaultHost host)
        {
            ps.Streams.Debug.DataAdded += Debug_DataAdded;
            ps.Streams.Error.DataAdded += Error_DataAdded;
            ps.Streams.Progress.DataAdded += Progress_DataAdded;

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
                            MethodInfo method = GetType().GetMethod("Information_DataAdded", BindingFlags.Instance | BindingFlags.Public);
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
                host.OnInformation += Host_OnInformation;
            }

            ps.Streams.Verbose.DataAdded += Verbose_DataAdded;
            ps.Streams.Warning.DataAdded += Warning_DataAdded;
        }

        /// <summary>
        /// Executes PowerShell script asynchronously with cancellation token support and semaphore throttling
        /// </summary>
        public async Task<PowerShellExecutionResult> ExecutePowerShellAsync(string script, bool isInlinePowershell, CancellationToken cancellationToken = default)
        {
            // Ensure semaphore is initialized
            if (_executionSemaphore == null)
            {
                var settings = SettingsService.Instance.Settings;
                InitializeSemaphore(settings.MaxConcurrentRunspaces);
            }

            // Wait for semaphore slot (throttles concurrent executions)
            await _executionSemaphore!.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                // Execute the synchronous PowerShell invocation in a background task
                // This prevents blocking the IoT Hub listener thread
                return await Task.Run(() => ExecutePowerShellInternal(script, isInlinePowershell, cancellationToken), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                // Always release semaphore slot
                _executionSemaphore.Release();
            }
        }

        /// <summary>
        /// Executes PowerShell script synchronously (kept for backward compatibility)
        /// For new code, prefer ExecutePowerShellAsync
        /// </summary>
        public PowerShellExecutionResult ExecutePowerShell(string script, bool isInlinePowershell)
        {
            // For backward compatibility, call async version synchronously
            // Note: This will still block, but allows gradual migration
            return ExecutePowerShellAsync(script, isInlinePowershell, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Internal method that performs the actual PowerShell execution
        /// </summary>
        private PowerShellExecutionResult ExecutePowerShellInternal(string script, bool isInlinePowershell, CancellationToken cancellationToken)
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

                // Check for cancellation before starting
                cancellationToken.ThrowIfCancellationRequested();

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
                                    SendLog($"Module preload warning: {errorMsg}", LogOutputType.Warning);
                                }
                            }
                        }
                    } // preloadPs.Dispose() called here automatically
                }
                catch (Exception ex)
                {
                    // Non-fatal - continue execution as modules may still work
                    SendLog($"Module preload exception (non-fatal): {ex.Message}", LogOutputType.Warning);
                }

                // Check for cancellation before adding script
                cancellationToken.ThrowIfCancellationRequested();

                powerShell.AddScript(script, false);

                PSInvocationSettings psinvocationSettings = new PSInvocationSettings();
                psinvocationSettings.Host = defaultHost;

                // Invoke synchronously - this is wrapped in Task.Run by the caller
                // Note: PowerShell.Invoke() doesn't support cancellation directly, but Task.Run allows
                // the cancellation token to be checked before/after invocation
                Collection<PSObject> output = powerShell.Invoke();

                // Check for cancellation after invocation
                cancellationToken.ThrowIfCancellationRequested();

                // Debug: Log output count for troubleshooting
                if (output.Count == 0)
                {
                    SendLog($"PowerShell output collection is empty (Count=0). Output may be going to host streams.", LogOutputType.Debug);
                }
                else
                {
                    SendLog($"PowerShell output collection has {output.Count} object(s)", LogOutputType.Debug);
                }

                // Check for errors - collect all error messages with full exception details
                if (powerShell.Streams.Error.Count > 0)
                {
                    var errorDetailsList = new List<PowerShellErrorDetails>();
                    StringBuilder errorBuilder = new StringBuilder();
                    
                    foreach (ErrorRecord error in powerShell.Streams.Error)
                    {
                        // Create structured error details with full exception information
                        var errorDetails = PowerShellErrorDetails.FromErrorRecord(error);
                        errorDetailsList.Add(errorDetails);

                        // Build human-readable error message
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

                        // Include inner exception messages in the summary
                        var innerException = error.Exception?.InnerException;
                        int innerDepth = 0;
                        while (innerException != null && innerDepth < 5)
                        {
                            errorBuilder.AppendLine($"  Inner Exception: {innerException.Message}");
                            innerException = innerException.InnerException;
                            innerDepth++;
                        }
                    }
                    
                    result.ErrorMessage = errorBuilder.ToString();
                    result.ErrorDetails = errorDetailsList;
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

                // Handle multiple output objects by collecting them into an array
                object? outputToSerialize = null;
                if (output.Count > 1)
                {
                    // Multiple objects - serialize as array
                    var outputArray = new object[output.Count];
                    for (int i = 0; i < output.Count; i++)
                    {
                        outputArray[i] = output[i].BaseObject ?? output[i];
                    }
                    outputToSerialize = outputArray;
                    result.RawOutput = outputArray;
                    
                    // Debug: Log array serialization
                    SendLog($"Serializing array of {output.Count} objects, first object type: {outputArray[0]?.GetType().FullName ?? "null"}", LogOutputType.Debug);
                }
                else if (output.Count == 1)
                {
                    // Single object
                    psobject2 = output[0];
                    result.RawOutput = psobject2;
                    outputToSerialize = psobject2.BaseObject ?? psobject2;
                    
                    // Debug: Log the type being serialized
                    if (outputToSerialize != null)
                    {
                        SendLog($"Serializing single object of type: {outputToSerialize.GetType().FullName}, IsComplex: {IsComplexObject(outputToSerialize)}", LogOutputType.Debug);
                    }
                }

                if (outputToSerialize != null)
                {
                    try
                    {
                        // For complex objects (arrays, collections, objects with properties), serialize to JSON
                        // For simple types (strings, numbers, dates), use ToString()
                        if (IsComplexObject(outputToSerialize))
                        {
                            // Serialize complex objects to compressed JSON for efficient transmission
                            var context = new ConvertToJsonContext(
                                maxDepth: 1024,
                                enumsAsStrings: true,
                                compressOutput: true); // Compress JSON (no indentation) for smaller payload
                            string jsonOutput = JsonObject.ConvertToJson(outputToSerialize, context);
                            result.Output = !string.IsNullOrEmpty(jsonOutput) ? jsonOutput : string.Empty;
                        }
                        else
                        {
                            // Use ToString() for simple types (DateTime, string, numbers, etc.)
                            string stringOutput = outputToSerialize.ToString();
                            result.Output = !string.IsNullOrEmpty(stringOutput) ? stringOutput : string.Empty;
                        }
                        result.Success = true;
                    }
                    catch (Exception jsonEx)
                    {
                        // If JSON serialization fails, fall back to ToString()
                        SendLog($"JSON serialization failed, using ToString(): {jsonEx.Message}", LogOutputType.Warning);
                        try
                        {
                            result.Output = outputToSerialize.ToString() ?? string.Empty;
                            result.Success = true;
                        }
                        catch
                        {
                            result.Output = string.Empty;
                            result.Success = true;
                        }
                    }
                }
                else if (string.IsNullOrEmpty(result.ErrorMessage))
                {
                    result.Success = true;
                    result.Output = string.Empty;
                }
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.ErrorMessage = "Script execution was cancelled.";
                SendLog("Script execution was cancelled.", LogOutputType.Warning);
                throw; // Re-throw to propagate cancellation
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Exception = ex;
                result.ErrorMessage = ex.ToString();
                
                // Capture structured error details including inner exceptions
                result.ErrorDetails = new List<PowerShellErrorDetails>
                {
                    PowerShellErrorDetails.FromException(ex)
                };
                
                SendLog(ex.ToString(), LogOutputType.Error);
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

        /// <summary>
        /// Determines if an object is complex and should be serialized to JSON
        /// Simple types (strings, numbers, dates, booleans) return false
        /// Complex types (arrays, collections, objects with properties) return true
        /// </summary>
        private static bool IsComplexObject(object obj)
        {
            if (obj == null)
                return false;

            Type type = obj.GetType();

            // Simple types that should use ToString()
            if (type.IsPrimitive || 
                type == typeof(string) || 
                type == typeof(DateTime) || 
                type == typeof(DateTimeOffset) ||
                type == typeof(TimeSpan) ||
                type == typeof(Guid) ||
                type == typeof(decimal) ||
                type.IsEnum)
            {
                return false;
            }

            // Arrays and collections are complex
            if (obj is IEnumerable && !(obj is string))
            {
                return true;
            }

            // Objects with properties are complex (but not simple value types)
            if (type.IsClass && type != typeof(object))
            {
                // Check if it has properties beyond the base object
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                if (properties.Length > 0)
                {
                    return true;
                }
            }

            // PSObject wrappers are complex if they have properties
            if (obj is PSObject psObj)
            {
                var properties = psObj.Properties;
                if (properties != null)
                {
                    // Check if collection has any items by iterating
                    foreach (var prop in properties)
                    {
                        return true; // Has at least one property, so it's complex
                    }
                }
            }

            return false;
        }

        public void Dispose()
        {
            // Cleanup if needed
            if (_logCallback is IDisposable disposable)
            {
                disposable.Dispose();
            }
            GC.SuppressFinalize(this);
            // Note: Do not call GC.Collect() - let the GC manage itself
            // Calling GC.Collect() can cause performance issues and is an anti-pattern
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
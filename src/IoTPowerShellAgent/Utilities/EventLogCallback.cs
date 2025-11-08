using System;
using System.IO;
using IoTPowerShellAgent.Core;
using IoTPowerShellAgent.PowerShell;

namespace IoTPowerShellAgent.Utilities
{
    /// <summary>
    /// Log callback that writes to Windows Event Log.
    /// Can wrap another ILogCallback to write to both event log and the wrapped callback
    /// </summary>
    public class EventLogCallback : ILogCallback, IDisposable
    {
        private readonly WindowsEventLogger _eventLogger;
        private readonly ILogCallback? _wrappedCallback;
        private bool _disposed = false;

        /// <summary>
        /// Creates an event log callback
        /// </summary>
        /// <param name="eventSourceName">Event source name (e.g., "IoTPowerShellAgent")</param>
        /// <param name="wrappedCallback">Optional callback to also forward logs to (e.g., IoT Hub)</param>
        /// <param name="outputWriter">Optional text writer for console output</param>
        public EventLogCallback(string eventSourceName, ILogCallback? wrappedCallback = null, TextWriter? outputWriter = null)
        {
            _wrappedCallback = wrappedCallback;
            _eventLogger = WindowsEventLogger.Create(eventSourceName, outputWriter);
        }

        /// <summary>
        /// ILogCallback implementation - writes to event log and optionally forwards to wrapped callback
        /// </summary>
        public void OnLog(string message, LogOutputType logType)
        {
            if (string.IsNullOrEmpty(message))
                return;

            try
            {
                // Write to Windows Event Log
                _eventLogger.Write(message, logType);
            }
            catch
            {
                // Silently fail if event log write fails to avoid breaking the application
            }

            // Forward to wrapped callback if provided
            try
            {
                _wrappedCallback?.OnLog(message, logType);
            }
            catch
            {
                // Silently fail if wrapped callback fails
            }
        }

        /// <summary>
        /// Writes an error message (convenience)
        /// </summary>
        public void WriteError(string message)
        {
            OnLog(message, LogOutputType.Error);
        }

        /// <summary>
        /// Writes a warning message (convenience)
        /// </summary>
        public void WriteWarning(string message)
        {
            OnLog(message, LogOutputType.Warning);
        }

        /// <summary>
        /// Writes an info message (convenience)
        /// </summary>
        public void WriteInfo(string message)
        {
            OnLog(message, LogOutputType.Information);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _eventLogger?.Dispose();
                _disposed = true;
            }
        }
    }
}


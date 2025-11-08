using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using Microsoft.Win32;
using IoTPowerShellAgent.Core;

namespace IoTPowerShellAgent.Utilities
{
    /// <summary>
    /// Windows Event Log logger
    /// </summary>
    public class WindowsEventLogger : IDisposable
    {
        private readonly EventLog _eventLog;
        private readonly TextWriter? _outputWriter;
        private bool _disposed = false;

        // Event IDs matching Go implementation
        private const int InfoEventId = 100;
        private const int WarningEventId = 200;
        private const int ErrorEventId = 300;

        private WindowsEventLogger(string eventSourceName, TextWriter? outputWriter = null)
        {
            _outputWriter = outputWriter;
            _eventLog = new EventLog("Application")
            {
                Source = eventSourceName
            };
        }

        /// <summary>
        /// Creates a new Windows Event Logger
        /// </summary>
        public static WindowsEventLogger Create(string eventSourceName, TextWriter? outputWriter = null)
        {
            // Check if event source exists and create if needed
            if (!EventSourceExists(eventSourceName))
            {
                try
                {
                    // Install event source
                    EventLog.CreateEventSource(eventSourceName, "Application");
                }
                catch (SecurityException)
                {
                    // If we don't have permission, try to continue - the source might already exist
                    // or we might be able to write without creating it
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
                {
                    // Source might already exist or invalid name - continue
                }
            }

            // Open event log
            return new WindowsEventLogger(eventSourceName, outputWriter);
        }

        /// <summary>
        /// Checks if event source exists in registry
        /// </summary>
        private static bool EventSourceExists(string eventSourceName)
        {
            try
            {
                // Check registry key
                string registryPath = @"SYSTEM\CurrentControlSet\Services\EventLog\Application\" + eventSourceName;

                using (var key = Registry.LocalMachine.OpenSubKey(registryPath, false))
                {
                    return key != null;
                }
            }
            catch
            {
                // If we can't check registry, assume it doesn't exist
                return false;
            }
        }

        /// <summary>
        /// Writes a log entry
        /// </summary>
        public void Write(string message, LogOutputType logType)
        {
            if (string.IsNullOrEmpty(message))
                return;

            // Extract clean message (remove log level prefix if present)
            string cleanMessage = ExtractMessage(message);

            // Write to event log based on log type
            switch (logType)
            {
                case LogOutputType.Error:
                    _eventLog.WriteEntry(cleanMessage, EventLogEntryType.Error, ErrorEventId);
                    break;

                case LogOutputType.Warning:
                    _eventLog.WriteEntry(cleanMessage, EventLogEntryType.Warning, WarningEventId);
                    break;

                case LogOutputType.Information:
                case LogOutputType.Verbose:
                case LogOutputType.Debug:
                case LogOutputType.Progress:
                default:
                    _eventLog.WriteEntry(cleanMessage, EventLogEntryType.Information, InfoEventId);
                    break;
            }

            // Also write to original output if provided
            _outputWriter?.WriteLine(message);
        }

        /// <summary>
        /// Writes an error message
        /// </summary>
        public void WriteError(string message)
        {
            Write(message, LogOutputType.Error);
        }

        /// <summary>
        /// Writes a warning message
        /// </summary>
        public void WriteWarning(string message)
        {
            Write(message, LogOutputType.Warning);
        }

        /// <summary>
        /// Writes an info message
        /// </summary>
        public void WriteInfo(string message)
        {
            Write(message, LogOutputType.Information);
        }

        /// <summary>
        /// Extracts clean message from log line
        /// </summary>
        private static string ExtractMessage(string line)
        {
            // Remove common log prefixes like [ERROR], [WARNING], [INFO], etc.
            string message = line;

            // Remove [LOGLEVEL] prefixes
            int bracketIndex = message.IndexOf(']');
            if (bracketIndex > 0 && message[0] == '[')
            {
                message = message.Substring(bracketIndex + 1).TrimStart();
            }

            // Remove timestamp prefixes if present (format: "2024-01-01 12:00:00 [LEVEL]")
            if (message.Length > 19 && message[4] == '-' && message[7] == '-' && message[10] == ' ')
            {
                int spaceAfterTimestamp = message.IndexOf(' ', 10);
                if (spaceAfterTimestamp > 0 && spaceAfterTimestamp < 30)
                {
                    message = message.Substring(spaceAfterTimestamp + 1).TrimStart();
                }
            }

            return message.Trim();
        }

        /// <summary>
        /// Closes the event log
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _eventLog?.Close();
                _disposed = true;
            }
        }
    }
}


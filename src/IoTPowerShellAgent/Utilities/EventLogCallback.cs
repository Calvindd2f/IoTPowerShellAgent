using System;
using System.IO;
using IoTPowerShellAgent.Core;
using IoTPowerShellAgent.PowerShell;

namespace IoTPowerShellAgent.Utilities
{
    public class EventLogCallback : ILogCallback, IDisposable
    {
        private readonly WindowsEventLogger _eventLogger;
        private readonly ILogCallback? _wrappedCallback;
        private bool _disposed = false;
        public EventLogCallback(string eventSourceName, ILogCallback? wrappedCallback = null, TextWriter? outputWriter = null)
        {
            _wrappedCallback = wrappedCallback;
            _eventLogger = WindowsEventLogger.Create(eventSourceName, outputWriter);
        }
        public void OnLog(string message, LogOutputType logType)
        {
            if (string.IsNullOrEmpty(message))
                return;

            try
            {

                _eventLogger.Write(message, logType);
            }
            catch
            {

            }


            try
            {
                _wrappedCallback?.OnLog(message, logType);
            }
            catch
            {

            }
        }
        public void WriteError(string message)
        {
            OnLog(message, LogOutputType.Error);
        }
        public void WriteWarning(string message)
        {
            OnLog(message, LogOutputType.Warning);
        }
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


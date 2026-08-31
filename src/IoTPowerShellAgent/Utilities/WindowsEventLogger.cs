using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using Microsoft.Win32;
using IoTPowerShellAgent.Core;

namespace IoTPowerShellAgent.Utilities
{



    public class WindowsEventLogger : IDisposable
    {
        private readonly EventLog _eventLog;
        private readonly TextWriter? _outputWriter;
        private bool _disposed = false;


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




        public static WindowsEventLogger Create(string eventSourceName, TextWriter? outputWriter = null)
        {

            if (!EventSourceExists(eventSourceName))
            {
                try
                {

                    EventLog.CreateEventSource(eventSourceName, "Application");
                }
                catch (SecurityException)
                {


                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
                {

                }
            }


            return new WindowsEventLogger(eventSourceName, outputWriter);
        }




        private static bool EventSourceExists(string eventSourceName)
        {
            try
            {

                string registryPath = @"SYSTEM\CurrentControlSet\Services\EventLog\Application\" + eventSourceName;

                using (var key = Registry.LocalMachine.OpenSubKey(registryPath, false))
                {
                    return key != null;
                }
            }
            catch
            {

                return false;
            }
        }




        public void Write(string message, LogOutputType logType)
        {
            if (string.IsNullOrEmpty(message))
                return;


            string cleanMessage = ExtractMessage(message);


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


            _outputWriter?.WriteLine(message);
        }




        public void WriteError(string message)
        {
            Write(message, LogOutputType.Error);
        }




        public void WriteWarning(string message)
        {
            Write(message, LogOutputType.Warning);
        }




        public void WriteInfo(string message)
        {
            Write(message, LogOutputType.Information);
        }




        private static string ExtractMessage(string line)
        {

            string message = line;


            int bracketIndex = message.IndexOf(']');
            if (bracketIndex > 0 && message[0] == '[')
            {
                message = message.Substring(bracketIndex + 1).TrimStart();
            }


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


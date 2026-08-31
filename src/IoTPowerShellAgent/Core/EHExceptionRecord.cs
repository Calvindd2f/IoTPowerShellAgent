using System;

namespace IoTPowerShellAgent.Core
{
    public readonly struct EHExceptionRecord
    {
        public string Message { get; }
        public string StackTrace { get; }
        public string Source { get; }
        public DateTime Timestamp { get; }
        public Exception? InnerException { get; }

        public EHExceptionRecord(Exception ex)
        {
            if (ex == null)
            {
                throw new ArgumentNullException(nameof(ex));
            }

            Message = ex.Message ?? string.Empty;
            StackTrace = ex.StackTrace ?? string.Empty;
            Source = ex.Source ?? string.Empty;
            Timestamp = DateTime.UtcNow;
            InnerException = ex.InnerException;
        }
    }
}
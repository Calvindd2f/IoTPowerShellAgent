using System;

namespace IoTPowerShellAgent.Core
{
    /// <summary>
    /// Represents an exception record with additional metadata
    /// </summary>
    public readonly struct EHExceptionRecord
    {
        public string Message { get; }
        public string StackTrace { get; }
        public string Source { get; }
        public DateTime Timestamp { get; }
        public Exception? InnerException { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="EHExceptionRecord"/> struct.
        /// </summary>
        /// <param name="ex">The exception to initialize the record with.</param>
        /// <exception cref="ArgumentNullException">Thrown if the <paramref name="ex"/> parameter is null.</exception>
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
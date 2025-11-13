using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Text.Json.Serialization;

namespace IoTPowerShellAgent.Core
{
    /// <summary>
    /// Structured error details from PowerShell ErrorRecord with full exception information
    /// </summary>
    public class PowerShellErrorDetails
    {
        /// <summary>
        /// The error message
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// The exception type name
        /// </summary>
        [JsonPropertyName("exceptionType")]
        public string? ExceptionType { get; set; }

        /// <summary>
        /// Full exception details including inner exceptions
        /// </summary>
        [JsonPropertyName("exceptionDetails")]
        public string? ExceptionDetails { get; set; }

        /// <summary>
        /// The exception stack trace
        /// </summary>
        [JsonPropertyName("stackTrace")]
        public string? StackTrace { get; set; }

        /// <summary>
        /// PowerShell script stack trace
        /// </summary>
        [JsonPropertyName("scriptStackTrace")]
        public string? ScriptStackTrace { get; set; }

        /// <summary>
        /// The error category
        /// </summary>
        [JsonPropertyName("category")]
        public string? Category { get; set; }

        /// <summary>
        /// The fully qualified error ID
        /// </summary>
        [JsonPropertyName("fullyQualifiedErrorId")]
        public string? FullyQualifiedErrorId { get; set; }

        /// <summary>
        /// The target object that caused the error
        /// </summary>
        [JsonPropertyName("targetObject")]
        public string? TargetObject { get; set; }

        /// <summary>
        /// Inner exception details (recursive)
        /// </summary>
        [JsonPropertyName("innerException")]
        public PowerShellErrorDetails? InnerException { get; set; }

        /// <summary>
        /// Creates a PowerShellErrorDetails from an ErrorRecord
        /// </summary>
        public static PowerShellErrorDetails FromErrorRecord(ErrorRecord errorRecord)
        {
            var details = new PowerShellErrorDetails
            {
                Message = errorRecord.Exception?.Message ?? errorRecord.ToString(),
                ExceptionType = errorRecord.Exception?.GetType().FullName,
                Category = errorRecord.CategoryInfo?.Category.ToString(),
                FullyQualifiedErrorId = errorRecord.FullyQualifiedErrorId,
                TargetObject = errorRecord.TargetObject?.ToString(),
                ScriptStackTrace = errorRecord.ScriptStackTrace
            };

            // Capture full exception details including inner exceptions
            if (errorRecord.Exception != null)
            {
                details.ExceptionDetails = GetFullExceptionDetails(errorRecord.Exception);
                details.StackTrace = errorRecord.Exception.StackTrace;
                
                // Recursively capture inner exceptions
                if (errorRecord.Exception.InnerException != null)
                {
                    details.InnerException = FromException(errorRecord.Exception.InnerException);
                }
            }

            return details;
        }

        /// <summary>
        /// Creates a PowerShellErrorDetails from an Exception
        /// </summary>
        public static PowerShellErrorDetails FromException(Exception exception)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            var details = new PowerShellErrorDetails
            {
                Message = exception.Message,
                ExceptionType = exception.GetType().FullName,
                ExceptionDetails = GetFullExceptionDetails(exception),
                StackTrace = exception.StackTrace
            };

            // Recursively capture inner exceptions
            if (exception.InnerException != null)
            {
                details.InnerException = FromException(exception.InnerException);
            }

            return details;
        }

        /// <summary>
        /// Gets full exception details including all inner exceptions
        /// </summary>
        private static string GetFullExceptionDetails(Exception exception)
        {
            if (exception == null)
                return string.Empty;

            var details = new System.Text.StringBuilder();
            var currentException = exception;
            int depth = 0;

            while (currentException != null && depth < 10) // Limit depth to prevent infinite loops
            {
                if (depth > 0)
                {
                    details.AppendLine($"--- Inner Exception (Depth {depth}) ---");
                }

                details.AppendLine($"Type: {currentException.GetType().FullName}");
                details.AppendLine($"Message: {currentException.Message}");
                
                if (!string.IsNullOrEmpty(currentException.StackTrace))
                {
                    details.AppendLine($"StackTrace: {currentException.StackTrace}");
                }

                if (!string.IsNullOrEmpty(currentException.Source))
                {
                    details.AppendLine($"Source: {currentException.Source}");
                }

                // Capture additional properties if available
                if (currentException is System.Management.Automation.RuntimeException psException)
                {
                    if (!string.IsNullOrEmpty(psException.ErrorRecord?.ScriptStackTrace))
                    {
                        details.AppendLine($"ScriptStackTrace: {psException.ErrorRecord.ScriptStackTrace}");
                    }
                }

                currentException = currentException.InnerException;
                depth++;
            }

            return details.ToString();
        }
    }
}


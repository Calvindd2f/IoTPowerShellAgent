using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Text.Json.Serialization;

namespace IoTPowerShellAgent.Core
{
    public class PowerShellErrorDetails
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("exceptionType")]
        public string? ExceptionType { get; set; }

        [JsonPropertyName("exceptionDetails")]
        public string? ExceptionDetails { get; set; }

        [JsonPropertyName("stackTrace")]
        public string? StackTrace { get; set; }

        [JsonPropertyName("scriptStackTrace")]
        public string? ScriptStackTrace { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("fullyQualifiedErrorId")]
        public string? FullyQualifiedErrorId { get; set; }

        [JsonPropertyName("targetObject")]
        public string? TargetObject { get; set; }

        [JsonPropertyName("innerException")]
        public PowerShellErrorDetails? InnerException { get; set; }

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

            if (errorRecord.Exception != null)
            {
                details.ExceptionDetails = GetFullExceptionDetails(errorRecord.Exception);
                details.StackTrace = errorRecord.Exception.StackTrace;
                
                if (errorRecord.Exception.InnerException != null)
                {
                    details.InnerException = FromException(errorRecord.Exception.InnerException);
                }
            }

            return details;
        }

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

            if (exception.InnerException != null)
            {
                details.InnerException = FromException(exception.InnerException);
            }

            return details;
        }

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


using System;
using System.Collections.Generic;
using IoTPowerShellAgent.Utilities;

namespace IoTPowerShellAgent.Core
{
    public class PowerShellExecutionResult
    {
        public bool Success { get; set; }

        public string Output { get; set; }

        public string ErrorMessage { get; set; }

        public List<PowerShellErrorDetails>? ErrorDetails { get; set; }

        public Exception? Exception { get; set; }

        public object? RawOutput { get; set; }

        public PowerShellExecutionResult()
        {
            Success = false;
            Output = string.Empty;
            ErrorMessage = string.Empty;
        }

        public string ToJson(bool compressOutput = false)
        {
            try
            {
                var context = new ConvertToJsonContext(
                    maxDepth: 1024,
                    enumsAsStrings: true,
                    compressOutput: compressOutput);

                var resultObject = new
                {
                    Success,
                    Output,
                    ErrorMessage,
                    ErrorDetails,
                    Exception = Exception != null ? PowerShellErrorDetails.FromException(Exception) : null,
                    RawOutput = RawOutput
                };

                return JsonObject.ConvertToJson(resultObject, context) ?? "{}";
            }
            catch
            {
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    Success,
                    Output,
                    ErrorMessage,
                    Exception = Exception?.ToString()
                });
            }
        }
    }
}

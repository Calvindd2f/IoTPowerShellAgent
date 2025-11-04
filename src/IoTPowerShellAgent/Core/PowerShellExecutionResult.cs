using System;
using IoTPowerShellAgent.Utilities;

namespace IoTPowerShellAgent.Core
{
    /// <summary>
    /// Represents the result of a PowerShell script execution
    /// </summary>
    public class PowerShellExecutionResult
    {
        /// <summary>
        /// Indicates whether the execution was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The output result as a string
        /// </summary>
        public string Output { get; set; }

        /// <summary>
        /// Any error message if execution failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// The exception that occurred during execution, if any
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// The raw output object from PowerShell
        /// </summary>
        public object? RawOutput { get; set; }

        public PowerShellExecutionResult()
        {
            Success = false;
            Output = string.Empty;
            ErrorMessage = string.Empty;
        }

        /// <summary>
        /// Converts the result to JSON string using the PowerShell JSON utilities
        /// </summary>
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
                    Exception = Exception?.ToString(),
                    RawOutput = RawOutput
                };

                return JsonObject.ConvertToJson(resultObject, context) ?? "{}";
            }
            catch
            {
                // Fallback to simple JSON serialization
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

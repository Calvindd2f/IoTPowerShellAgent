namespace IoTPowerShellAgent.Core
{
    /// <summary>
    /// Represents the type of log output from PowerShell execution
    /// </summary>
    public enum LogOutputType
    {
        Error = 1,
        Verbose = 2,
        Information = 3,
        Warning = 4,
        Debug = 5,
        Progress = 6
    }
}

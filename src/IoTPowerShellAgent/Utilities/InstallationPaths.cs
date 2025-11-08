using System;
using System.IO;

namespace IoTPowerShellAgent.Utilities
{
    /// <summary>
    /// Utility class for managing installation paths.
    /// </summary>
    public static class InstallationPaths
    {
        /// <summary>
        /// Gets the program directory based on organization ID
        /// </summary>
        public static string GetProgramDirectory(string orgId)
        {
            string programFilesDir = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            return Path.Combine(programFilesDir, "IoTPowerShellAgent", orgId);
        }

        /// <summary>
        /// Gets the data directory based on organization ID
        /// </summary>
        public static string GetDataDirectory(string orgId)
        {
            string programDataDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            return Path.Combine(programDataDir, "IoTPowerShellAgent", orgId);
        }

        /// <summary>
        /// Gets the scripts directory based on organization ID
        /// </summary>
        public static string GetScriptsDirectory(string orgId)
        {
            string systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            return Path.Combine(systemDrive, "IoTPowerShellAgent", "scripts", orgId);
        }

        /// <summary>
        /// Gets the agent executable path
        /// </summary>
        public static string GetAgentExecutablePath(string orgId)
        {
            return Path.Combine(GetProgramDirectory(orgId), "IoTPowerShellAgent.exe");
        }

        /// <summary>
        /// Gets the service executable path
        /// </summary>
        public static string GetServiceExecutablePath(string orgId)
        {
            return GetAgentExecutablePath(orgId);
        }

        /// <summary>
        /// Gets the service manager path
        /// </summary>
        public static string GetServiceManagerPath(string orgId)
        {
            return GetAgentExecutablePath(orgId);
        }

        /// <summary>
        /// Gets the configuration file path
        /// </summary>
        public static string GetConfigFilePath(string orgId)
        {
            return Path.Combine(GetDataDirectory(orgId), "appsettings.json");
        }

        /// <summary>
        /// Gets the log file path
        /// </summary>
        public static string GetLogFilePath(string orgId)
        {
            return Path.Combine(GetDataDirectory(orgId), "iot_powershell_agent.log");
        }

        /// <summary>
        /// Gets the service name based on organization ID
        /// </summary>
        public static string GetServiceName(string orgId)
        {
            return $"IoTPowerShellAgent_{orgId}";
        }

        /// <summary>
        /// Creates all required directories for the installation
        /// </summary>
        public static void CreateDirectories(string orgId)
        {
            Directory.CreateDirectory(GetProgramDirectory(orgId));
            Directory.CreateDirectory(GetDataDirectory(orgId));
            Directory.CreateDirectory(GetScriptsDirectory(orgId));
        }
    }
}


using System;
using IoTPowerShellAgent.Utilities;
using static IoTPowerShellAgent.Utilities.WindowsApiInterop;

namespace IoTPowerShellAgent.Installation
{
    /// <summary>
    /// Helper class for service installation information
    /// Note: The actual installation is handled by InstallerProgram.cs using sc.exe
    /// This class provides helper methods for getting service information
    /// </summary>
    public static class ServiceInstallerHelper
    {
        /// <summary>
        /// Gets the organization ID from command line arguments or environment variable
        /// </summary>
        public static string GetOrgIdFromContext()
        {
            // Try to get from command line arguments
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals("/orgId", StringComparison.OrdinalIgnoreCase) ||
                    args[i].Equals("--orgId", StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            // Try environment variable (WinAPI for better performance)
            string? envOrgId = WindowsApiInterop.GetEnvironmentVariableNative("IOT_PS_AGENT_ORG_ID") ??
                            Environment.GetEnvironmentVariable("IOT_PS_AGENT_ORG_ID");
            if (!string.IsNullOrEmpty(envOrgId))
            {
                return envOrgId;
            }

            // Default to "default" if not specified
            return "default";
        }

        /// <summary>
        /// Gets service installation information
        /// </summary>
        public static ServiceInstallInfo GetServiceInfo(string orgId)
        {
            return new ServiceInstallInfo
            {
                OrgId = orgId,
                ServiceName = InstallationPaths.GetServiceName(orgId),
                DisplayName = $"IoT PowerShell Agent - {orgId}",
                Description = "Azure IoT Hub PowerShell execution agent service",
                ExecutablePath = InstallationPaths.GetAgentExecutablePath(orgId),
                ConfigPath = InstallationPaths.GetConfigFilePath(orgId),
                LogPath = InstallationPaths.GetLogFilePath(orgId)
            };
        }
    }

    /// <summary>
    /// Service installation information
    /// </summary>
    public class ServiceInstallInfo
    {
        public string OrgId { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string ConfigPath { get; set; } = string.Empty;
        public string LogPath { get; set; } = string.Empty;
    }
}


using System;
using IoTPowerShellAgent.Utilities;

namespace IoTPowerShellAgent.Installation
{
    public static class ServiceInstallerHelper
    {
        public static ServiceInstallInfo GetServiceInfo()
        {
            return new ServiceInstallInfo
            {
                ServiceName = InstallationPaths.GetServiceName(),
                DisplayName = "IoT PowerShell Agent",
                Description = "Azure IoT Hub PowerShell execution agent service",
                ExecutablePath = InstallationPaths.GetAgentExecutablePath(),
                ConfigPath = InstallationPaths.GetConfigFilePath(),
                LogPath = InstallationPaths.GetLogFilePath()
            };
        }
    }

    public class ServiceInstallInfo
    {
        public string ServiceName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string ConfigPath { get; set; } = string.Empty;
        public string LogPath { get; set; } = string.Empty;
    }
}

using System;
using System.IO;

namespace IoTPowerShellAgent.Utilities
{
    public static class InstallationPaths
    {
        public static string GetProgramDirectory()
        {
            string programFilesDir = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            return Path.Combine(programFilesDir, "IoTPowerShellAgent");
        }

        public static string GetDataDirectory()
        {
            string programDataDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            return Path.Combine(programDataDir, "IoTPowerShellAgent");
        }

        public static string GetScriptsDirectory()
        {
            string systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            return Path.Combine(systemDrive, "IoTPowerShellAgent", "scripts");
        }

        public static string GetAgentExecutablePath()
        {
            return Path.Combine(GetProgramDirectory(), "IoTPowerShellAgent.exe");
        }

        public static string GetServiceExecutablePath()
        {
            return GetAgentExecutablePath();
        }

        public static string GetServiceManagerPath()
        {
            return GetAgentExecutablePath();
        }

        public static string GetConfigFilePath()
        {
            return Path.Combine(GetDataDirectory(), "appsettings.json");
        }

        public static string GetLogFilePath()
        {
            return Path.Combine(GetDataDirectory(), "iot_powershell_agent.log");
        }

        public static string GetServiceName()
        {
            return "IoTPowerShellAgent";
        }

        public static void CreateDirectories()
        {
            Directory.CreateDirectory(GetProgramDirectory());
            Directory.CreateDirectory(GetDataDirectory());
            Directory.CreateDirectory(GetScriptsDirectory());
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using IoTPowerShellAgent.Core;
using IoTPowerShellAgent.Utilities;

namespace IoTPowerShellAgent.Installation
{

    public class InstallerProgram
    {
        public static int RunInstaller(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            string command = args[0].ToLowerInvariant();
            

            try
            {
                switch (command)
                {
                    case "install":
                        return InstallService();
                    case "uninstall":
                        return UninstallService();
                    case "start":
                        return StartService();
                    case "stop":
                        return StopService();
                    case "status":
                        return GetServiceStatus();
                    case "update":
                        return CheckForUpdates();
                    default:
                        Console.WriteLine($"Unknown command: {command}");
                        PrintUsage();
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        

        private static int InstallService()
        {
            Console.WriteLine("Installing IoT PowerShell Agent service");

            Console.WriteLine("Creating installation directories...");
            InstallationPaths.CreateDirectories();

            string targetExe = InstallationPaths.GetAgentExecutablePath();
            string sourceExe = Assembly.GetExecutingAssembly().Location;

            if (!File.Exists(targetExe) || !AreFilesSame(sourceExe, targetExe))
            {
                Console.WriteLine($"Copying executable to {targetExe}...");
                File.Copy(sourceExe, targetExe, true);
            }

            string configPath = InstallationPaths.GetConfigFilePath();
            if (!File.Exists(configPath))
            {
                string sourceConfig = Path.Combine(
                    Path.GetDirectoryName(sourceExe) ?? "",
                    "appsettings.json");

                if (File.Exists(sourceConfig))
                {
                    Console.WriteLine($"Copying configuration file to {configPath}...");
                    File.Copy(sourceConfig, configPath, false);
                }
                else
                {
                    Console.WriteLine($"Warning: Source configuration file not found at {sourceConfig}");
                    Console.WriteLine("Creating default configuration file...");
                    CreateDefaultConfig(configPath);
                }
            }

            Console.WriteLine("Installing Windows Service...");
            string serviceName = InstallationPaths.GetServiceName();

            string scArgs = $"create \"{serviceName}\" binPath= \"{targetExe}\" start= auto DisplayName= \"IoT PowerShell Agent\"";
            int exitCode = RunCommand("sc.exe", scArgs);

            if (exitCode != 0)
            {
                throw new Exception($"Failed to install service. sc.exe returned exit code {exitCode}");
            }

            Console.WriteLine("Configuring service to run as NT AUTHORITY\\SYSTEM...");
            string configArgs = $"config \"{serviceName}\" obj= \"NT AUTHORITY\\SYSTEM\"";
            exitCode = RunCommand("sc.exe", configArgs);

            if (exitCode != 0)
            {
                Console.WriteLine($"Warning: Failed to configure service account. sc.exe returned exit code {exitCode}");
                Console.WriteLine("Service will run with default account. PowerShell scripts may not have SYSTEM privileges.");
            }
            else
            {
                Console.WriteLine("Service configured to run as NT AUTHORITY\\SYSTEM successfully.");
            }

            string descArgs = $"description \"{serviceName}\" \"Azure IoT Hub PowerShell execution agent service\"";
            RunCommand("sc.exe", descArgs);

            Console.WriteLine($"Service '{serviceName}' installed successfully.");
            Console.WriteLine($"Configuration file: {configPath}");
            Console.WriteLine($"Log file: {InstallationPaths.GetLogFilePath()}");
            Console.WriteLine("\nNote: Please update the configuration file with your IoT Hub connection string before starting the service.");

            return 0;
        }

        private static int UninstallService()
        {
            Console.WriteLine("Uninstalling IoT PowerShell Agent service");

            string serviceName = InstallationPaths.GetServiceName();
            string targetExe = InstallationPaths.GetAgentExecutablePath();

            try
            {
                using (var service = new ServiceController(serviceName))
                {
                    if (service.Status != ServiceControllerStatus.Stopped)
                    {
                        Console.WriteLine("Stopping service...");
                        service.Stop();
                        service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not stop service: {ex.Message}");
            }

            Console.WriteLine("Uninstalling Windows Service...");
            string deleteArgs = $"delete \"{serviceName}\"";
            int exitCode = RunCommand("sc.exe", deleteArgs);

            if (exitCode != 0)
            {
                throw new Exception($"Failed to uninstall service. sc.exe returned exit code {exitCode}");
            }

            Console.WriteLine($"Service '{serviceName}' uninstalled successfully.");

            return 0;
        }

        private static int StartService()
        {
            string serviceName = InstallationPaths.GetServiceName();

            using (var service = new ServiceController(serviceName))
            {
                if (service.Status == ServiceControllerStatus.Running)
                {
                    Console.WriteLine($"Service '{serviceName}' is already running.");
                    return 0;
                }

                Console.WriteLine($"Starting service '{serviceName}'...");
                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                Console.WriteLine($"Service '{serviceName}' started successfully.");
            }

            return 0;
        }

        private static int StopService()
        {
            string serviceName = InstallationPaths.GetServiceName();

            using (var service = new ServiceController(serviceName))
            {
                if (service.Status == ServiceControllerStatus.Stopped)
                {
                    Console.WriteLine($"Service '{serviceName}' is already stopped.");
                    return 0;
                }

                Console.WriteLine($"Stopping service '{serviceName}'...");
                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                Console.WriteLine($"Service '{serviceName}' stopped successfully.");
            }

            return 0;
        }

        private static int GetServiceStatus()
        {
            string serviceName = InstallationPaths.GetServiceName();

            try
            {
                using (var service = new ServiceController(serviceName))
                {
                    service.Refresh();
                    Console.WriteLine($"Service Name: {serviceName}");
                    Console.WriteLine($"Status: {service.Status}");
                    Console.WriteLine($"Display Name: {service.DisplayName}");
                    Console.WriteLine($"Executable Path: {InstallationPaths.GetAgentExecutablePath()}");
                    Console.WriteLine($"Config Path: {InstallationPaths.GetConfigFilePath()}");
                    Console.WriteLine($"Log Path: {InstallationPaths.GetLogFilePath()}");

                    Console.WriteLine("\nService Account Information:");
                    string qcArgs = $"qc \"{serviceName}\"";
                    var processStartInfo = new ProcessStartInfo
                    {
                        FileName = "sc.exe",
                        Arguments = qcArgs,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (var process = Process.Start(processStartInfo))
                    {
                        if (process != null)
                        {
                            string output = process.StandardOutput.ReadToEnd();
                            process.WaitForExit();

                            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var line in lines)
                            {
                                if (line.Contains("SERVICE_START_NAME", StringComparison.OrdinalIgnoreCase))
                                {
                                    Console.WriteLine($"  {line.Trim()}");

                                    if (line.Contains("NT AUTHORITY\\SYSTEM", StringComparison.OrdinalIgnoreCase))
                                    {
                                        Console.WriteLine("  ✓ PowerShell scripts will execute as NT AUTHORITY\\SYSTEM");
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Service '{serviceName}' is not installed or not accessible.");
                Console.WriteLine($"Error: {ex.Message}");
                return 1;
            }

            return 0;
        }

        private static bool AreFilesSame(string file1, string file2)
        {
            try
            {
                var info1 = new FileInfo(file1);
                var info2 = new FileInfo(file2);
                return info1.Length == info2.Length &&
                        info1.LastWriteTime == info2.LastWriteTime;
            }
            catch
            {
                return false;
            }
        }

        private static void CreateDefaultConfig(string configPath)
        {
            var defaultConfig = new
            {
                IoTHubConnectionString = "",
                DeviceId = "",
                ModuleId = "",
                ScriptTimeoutSeconds = 300
            };

            string json = System.Text.Json.JsonSerializer.Serialize(defaultConfig, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, json);
        }

        private static int RunCommand(string command, string arguments)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processStartInfo))
            {
                if (process == null)
                {
                    throw new Exception($"Failed to start process: {command}");
                }

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit();

                if (!string.IsNullOrEmpty(output))
                {
                    Console.WriteLine(output);
                }

                if (!string.IsNullOrEmpty(error) && process.ExitCode != 0)
                {
                    Console.Error.WriteLine(error);
                }

                return process.ExitCode;
            }
        }

        private static int CheckForUpdates()
        {
            Console.WriteLine("Checking for updates...");
            Console.WriteLine($"Current version: {Core.VersionInfo.Version}");

            try
            {
                var settings = Core.SettingsService.Instance.Settings;
                var updateInterval = TimeSpan.FromHours(settings.AutoUpdateIntervalHours);

                using (var updater = new AutoUpdater(
                    null,
                    settings.GitHubReleaseUrl,
                    enabled: true,
                    updateInterval: updateInterval))
                {
                    var result = updater.CheckForUpdatesAsync().GetAwaiter().GetResult();
                    if (result)
                    {
                        Console.WriteLine("Update check completed. Update was installed.");
                        return 0;
                    }
                    else
                    {
                        Console.WriteLine("No updates available or update check failed.");
                        return 1;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking for updates: {ex.Message}");
                return 1;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("IoT PowerShell Agent Installer");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  IoTPowerShellAgent.exe install [orgId]     Install the service");
            Console.WriteLine("  IoTPowerShellAgent.exe uninstall [orgId]   Uninstall the service");
            Console.WriteLine("  IoTPowerShellAgent.exe start [orgId]     Start the service");
            Console.WriteLine("  IoTPowerShellAgent.exe stop [orgId]       Stop the service");
            Console.WriteLine("  IoTPowerShellAgent.exe status [orgId]     Show service status");
            Console.WriteLine("  IoTPowerShellAgent.exe update [orgId]    Check for and install updates");
            Console.WriteLine();
            Console.WriteLine("If orgId is not specified, it defaults to 'default' or the value");
            Console.WriteLine("of the IOT_PS_AGENT_ORG_ID environment variable.");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  IoTPowerShellAgent.exe install myorg");
            Console.WriteLine("  IoTPowerShellAgent.exe start myorg");
            Console.WriteLine("  IoTPowerShellAgent.exe status myorg");
            Console.WriteLine("  IoTPowerShellAgent.exe update myorg");
        }
    }
}


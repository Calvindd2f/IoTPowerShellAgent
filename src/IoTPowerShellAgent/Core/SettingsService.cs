using System;
using System.IO;
using System.Text.Json;

namespace IoTPowerShellAgent.Core
{
    /// <summary>
    /// Service for managing application settings and configuration
    /// </summary>
    public class SettingsService
    {
        private static SettingsService? _instance;
        private static readonly object _lock = new object();
        private ServiceSettings? _settings;

        private SettingsService()
        {
            LoadSettings();
        }

        /// <summary>
        /// Gets the singleton instance of SettingsService
        /// </summary>
        public static SettingsService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new SettingsService();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Gets the current settings
        /// </summary>
        public ServiceSettings Settings
        {
            get
            {
                // Thread-safe: _settings is set in constructor and never null after LoadSettings()
                // If LoadSettings() fails, it sets _settings to a new instance
                return _settings ?? new ServiceSettings();
            }
        }

        /// <summary>
        /// Gets whether this is an activity node
        /// </summary>
        public bool GetIsActivityNode()
        {
            return Settings.IsActivityNode;
        }

        /// <summary>
        /// Loads settings from configuration file
        /// </summary>
                    private void LoadSettings()
            {
                try
                {
                    // Try config folder first, then base directory
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string configPath = Path.Combine(baseDir, "..", "..", "config", "appsettings.json");
                    if (!File.Exists(configPath))
                    {
                        configPath = Path.Combine(baseDir, "appsettings.json");
                    }
                    if (!File.Exists(configPath))
                    {
                        // Try relative to executable
                        configPath = Path.Combine(baseDir, "config", "appsettings.json");
                    }
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    _settings = JsonSerializer.Deserialize<ServiceSettings>(json);
                }
                else
                {
                    _settings = new ServiceSettings();
                    SaveSettings();
                }
            }
            catch
            {
                _settings = new ServiceSettings();
            }
        }

        /// <summary>
        /// Saves settings to configuration file
        /// </summary>
                    public void SaveSettings()
            {
                try
                {
                    // Try config folder first, then base directory
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string configPath = Path.Combine(baseDir, "..", "..", "config", "appsettings.json");
                    if (!Directory.Exists(Path.GetDirectoryName(configPath)))
                    {
                        configPath = Path.Combine(baseDir, "appsettings.json");
                    }
                    if (!Directory.Exists(Path.GetDirectoryName(configPath)))
                    {
                        configPath = Path.Combine(baseDir, "config", "appsettings.json");
                        Directory.CreateDirectory(Path.GetDirectoryName(configPath) ?? baseDir);
                    }
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_settings, options);
                File.WriteAllText(configPath, json);
            }
            catch
            {
                // Log error but don't throw
            }
        }
    }

    /// <summary>
    /// Application settings model
    /// </summary>
    public class ServiceSettings
    {
        /// <summary>
        /// Azure IoT Hub connection string
        /// </summary>
        public string IoTHubConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Device ID for IoT Hub
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Module ID for IoT Hub (if using modules)
        /// </summary>
        public string ModuleId { get; set; } = string.Empty;

        /// <summary>
        /// Whether this node is an activity node
        /// </summary>
        public bool IsActivityNode { get; set; } = false;

        /// <summary>
        /// Activity log threshold
        /// </summary>
        public int ActivityLogThreshold { get; set; } = 1000;

        /// <summary>
        /// Script execution timeout in seconds
        /// </summary>
        public int ScriptTimeoutSeconds { get; set; } = 300;

        /// <summary>
        /// Maximum number of concurrent PowerShell runspaces (default: 2)
        /// Used to throttle concurrent script executions and prevent OOM issues
        /// </summary>
        public int MaxConcurrentRunspaces { get; set; } = 2;

        /// <summary>
        /// Whether auto-updates are enabled
        /// </summary>
        public bool EnableAutoUpdates { get; set; } = true;

        /// <summary>
        /// GitHub repository URL for releases (e.g., https://api.github.com/repos/org/repo/releases/latest)
        /// </summary>
        public string? GitHubReleaseUrl { get; set; }

        /// <summary>
        /// Auto-update check interval in hours (default: 48)
        /// </summary>
        public int AutoUpdateIntervalHours { get; set; } = 48;

        /// <summary>
        /// Transport protocol to use (Amqp, Mqtt, Mqtt_WebSocket_Only). Default: Amqp
        /// </summary>
        public string TransportType { get; set; } = "Amqp";

        /// <summary>
        /// Azure IoT Hub hostname (alternative to connection string)
        /// </summary>
        public string? AzureIotHubHost { get; set; }

        /// <summary>
        /// Shared access key (alternative to connection string, base64 encoded)
        /// </summary>
        public string? SharedAccessKey { get; set; }
    }
}

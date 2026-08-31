using System;
using System.IO;
using System.Text.Json;

namespace IoTPowerShellAgent.Core
{
    public class SettingsService
    {
        private static SettingsService? _instance;
        private static readonly object _lock = new object();
        private ServiceSettings? _settings;

        private SettingsService()
        {
            LoadSettings();
        }

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

        public ServiceSettings Settings
        {
            get
            {
                // Thread-safe: _settings is set in constructor and never null after LoadSettings()
                // If LoadSettings() fails, it sets _settings to a new instance
                return _settings ?? new ServiceSettings();
            }
        }


                    private void LoadSettings()
            {
                try
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string configPath = Path.Combine(baseDir, "..", "..", "config", "appsettings.json");
                    if (!File.Exists(configPath))
                    {
                        configPath = Path.Combine(baseDir, "appsettings.json");
                    }
                    if (!File.Exists(configPath))
                    {
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

                    public void SaveSettings()
            {
                try
                {
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

    public class ServiceSettings
    {
        public string IoTHubConnectionString { get; set; } = string.Empty;

        public string DeviceId { get; set; } = string.Empty;

        public string ModuleId { get; set; } = string.Empty;


        public int ScriptTimeoutSeconds { get; set; } = 300;

        public int MaxConcurrentRunspaces { get; set; } = 2;

        public bool EnableAutoUpdates { get; set; } = true;

        public string? GitHubReleaseUrl { get; set; }

        public int AutoUpdateIntervalHours { get; set; } = 48;

        public string TransportType { get; set; } = "Amqp";

        public string? AzureIotHubHost { get; set; }

        public string? SharedAccessKey { get; set; }
    }
}

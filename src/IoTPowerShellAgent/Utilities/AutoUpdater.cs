using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using IoTPowerShellAgent.Core;
using IoTPowerShellAgent.Installation;
using IoTPowerShellAgent.PowerShell;
using IoTPowerShellAgent.Utilities;
using Microsoft.Extensions.Http;

namespace IoTPowerShellAgent.Utilities
{
    /// <summary>
    /// Auto-updater for checking and installing updates from GitHub releases
    /// </summary>
    public class AutoUpdater : IDisposable
    {
        private readonly ILogCallback? _logCallback;
        private readonly HttpClient _httpClient;
        private readonly string _latestReleaseUrl;
        private readonly TimeSpan _updateInterval;
        private readonly bool _enabled;
        private bool _disposed = false;
        private readonly bool _ownsHttpClient;

        // Static HttpClient factory for backward compatibility (when IHttpClientFactory not available)
        // This avoids socket exhaustion by reusing connections
        private static readonly Lazy<IHttpClientFactory> _defaultHttpClientFactory = new Lazy<IHttpClientFactory>(() => new DefaultHttpClientFactory());

        /// <summary>
        /// Creates AutoUpdater with IHttpClientFactory (recommended)
        /// </summary>
        public AutoUpdater(IHttpClientFactory httpClientFactory, ILogCallback? logCallback = null, string? githubRepoUrl = null, bool enabled = true, TimeSpan? updateInterval = null)
        {
            _logCallback = logCallback;
            _enabled = enabled;
            _updateInterval = updateInterval ?? TimeSpan.FromHours(48);

            // Default to a configurable repo URL, or use environment variable
            _latestReleaseUrl = githubRepoUrl ??
                WindowsApiInterop.GetEnvironmentVariableNative("IOT_PS_AGENT_GITHUB_REPO") ??
                "https://api.github.com/repos/Calvindd2f/IoTPowerShellAgent/releases/latest";

            // Use IHttpClientFactory for proper lifecycle management
            _httpClient = httpClientFactory.CreateClient("AutoUpdater");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "IoTPowerShellAgent");
            _ownsHttpClient = false; // Factory manages lifecycle
        }

        /// <summary>
        /// Creates AutoUpdater without IHttpClientFactory (backward compatibility)
        /// Uses a shared static HttpClient instance
        /// </summary>
        public AutoUpdater(ILogCallback? logCallback, string? githubRepoUrl = null, bool enabled = true, TimeSpan? updateInterval = null)
            : this(_defaultHttpClientFactory.Value, logCallback, githubRepoUrl, enabled, updateInterval)
        {
            // For backward compatibility, we still use the factory but mark that we own it
            // In practice, the factory will reuse the shared client
            _ownsHttpClient = false;
        }

        /// <summary>
        /// Checks for updates and installs if available
        /// </summary>
        public async Task<bool> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
        {
            if (!_enabled)
            {
                _logCallback?.OnLog("Auto-updater is disabled", LogOutputType.Information);
                return false;
            }

            try
            {
                _logCallback?.OnLog("Checking for updates...", LogOutputType.Information);

                var release = await FetchLatestReleaseAsync(cancellationToken).ConfigureAwait(false);
                if (release == null)
                {
                    return false;
                }

                _logCallback?.OnLog($"Latest release: {release.TagName}", LogOutputType.Information);

                string currentVersion = VersionInfo.Version;
                if (release.TagName == currentVersion || release.TagName == $"v{currentVersion}")
                {
                    _logCallback?.OnLog("No updates available", LogOutputType.Information);
                    return false;
                }

                var applicableAsset = FindApplicableAsset(release.Assets);
                if (applicableAsset == null)
                {
                    _logCallback?.OnLog($"No applicable asset found for Windows", LogOutputType.Warning);
                    return false;
                }

                _logCallback?.OnLog($"Update available: {release.TagName}. Downloading...", LogOutputType.Information);

                bool updated = await UpdateAsync(applicableAsset, cancellationToken).ConfigureAwait(false);
                if (updated)
                {
                    _logCallback?.OnLog($"Successfully updated to version {release.TagName}", LogOutputType.Information);
                }

                return updated;
            }
            catch (Exception ex)
            {
                _logCallback?.OnLog($"Error checking for updates: {ex.Message}", LogOutputType.Error);
                return false;
            }
        }

        /// <summary>
        /// Runs the auto-updater on a schedule
        /// </summary>
        public async Task RunAutoUpdaterAsync(CancellationToken cancellationToken)
        {
            _logCallback?.OnLog($"Auto-updater started. Current version: {VersionInfo.Version}. Check interval: {_updateInterval}", LogOutputType.Information);

            // Check immediately on start
            await CheckForUpdatesAsync(cancellationToken).ConfigureAwait(false);

            // Then check periodically
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_updateInterval, cancellationToken).ConfigureAwait(false);
                    await CheckForUpdatesAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logCallback?.OnLog($"Error in auto-updater loop: {ex.Message}", LogOutputType.Error);
                    // Continue running even if one check fails
                }
            }

            _logCallback?.OnLog("Auto-updater stopped", LogOutputType.Information);
        }

        private async Task<GitHubRelease?> FetchLatestReleaseAsync(CancellationToken cancellationToken)
        {
            try
            {
                var response = await _httpClient.GetAsync(_latestReleaseUrl, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    _logCallback?.OnLog($"Failed to fetch latest release. Status: {response.StatusCode}", LogOutputType.Error);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var release = JsonSerializer.Deserialize<GitHubRelease>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return release;
            }
            catch (Exception ex)
            {
                _logCallback?.OnLog($"Failed to fetch latest release: {ex.Message}", LogOutputType.Error);
                return null;
            }
        }

        private GitHubAsset? FindApplicableAsset(GitHubAsset[] assets)
        {
            foreach (var asset in assets)
            {
                // Look for Windows executable
                if (asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                    asset.Name.EndsWith(".win.exe", StringComparison.OrdinalIgnoreCase))
                {
                    return asset;
                }
            }

            return null;
        }

        private async Task<bool> UpdateAsync(GitHubAsset asset, CancellationToken cancellationToken)
        {
            try
            {
                string tempFile = await DownloadAssetAsync(asset, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrEmpty(tempFile))
                {
                    return false;
                }

                // Execute the update installer
                // The installer should handle stopping the service, updating files, and restarting
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = tempFile,
                    Arguments = "install",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _logCallback?.OnLog($"Running update installer: {tempFile}", LogOutputType.Information);

                using (var process = Process.Start(processStartInfo))
                {
                    if (process == null)
                    {
                        _logCallback?.OnLog("Failed to start update installer", LogOutputType.Error);
                        return false;
                    }

                    await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                    if (process.ExitCode == 0)
                    {
                        _logCallback?.OnLog("Update installer completed successfully", LogOutputType.Information);
                        return true;
                    }
                    else
                    {
                        _logCallback?.OnLog($"Update installer failed with exit code: {process.ExitCode}", LogOutputType.Error);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logCallback?.OnLog($"Error during update: {ex.Message}", LogOutputType.Error);
                return false;
            }
        }

        private async Task<string?> DownloadAssetAsync(GitHubAsset asset, CancellationToken cancellationToken)
        {
            try
            {
                _logCallback?.OnLog($"Downloading asset: {asset.Name}", LogOutputType.Information);

                var request = new HttpRequestMessage(HttpMethod.Get, asset.BrowserDownloadUrl);
                request.Headers.Add("Accept", "application/octet-stream");

                var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                // Create temporary file
                string tempFile = Path.Combine(Path.GetTempPath(), $"IoTPowerShellAgent-update-{Guid.NewGuid()}.exe");

                using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
                }

                _logCallback?.OnLog($"Downloaded to: {tempFile}", LogOutputType.Information);
                return tempFile;
            }
            catch (Exception ex)
            {
                _logCallback?.OnLog($"Failed to download asset: {ex.Message}", LogOutputType.Error);
                return null;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // IHttpClientFactory manages HttpClient lifecycle
                // Only dispose if we created it directly (which we don't in this implementation)
                if (_ownsHttpClient)
                {
                    _httpClient?.Dispose();
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// Default HttpClientFactory implementation for backward compatibility
        /// Uses a shared static HttpClient to avoid socket exhaustion
        /// </summary>
        private class DefaultHttpClientFactory : IHttpClientFactory
        {
            private static readonly Lazy<HttpClient> SharedHttpClient = new Lazy<HttpClient>(() =>
            {
                var client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(30)
                };
                client.DefaultRequestHeaders.Add("User-Agent", "IoTPowerShellAgent");
                return client;
            });

            public HttpClient CreateClient(string name)
            {
                // Return shared instance for all clients
                // This avoids socket exhaustion while maintaining compatibility
                return SharedHttpClient.Value;
            }
        }
    }

    /// <summary>
    /// GitHub release model
    /// </summary>
    public class GitHubRelease
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("assets")]
        public GitHubAsset[] Assets { get; set; } = Array.Empty<GitHubAsset>();
    }

    /// <summary>
    /// GitHub asset model
    /// </summary>
    public class GitHubAsset
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }
}


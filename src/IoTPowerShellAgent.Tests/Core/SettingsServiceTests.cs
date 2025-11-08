using System.IO;
using System.Text.Json;
using FluentAssertions;
using IoTPowerShellAgent.Core;
using Xunit;

namespace IoTPowerShellAgent.Tests.Core
{
    /// <summary>
    /// Unit tests for SettingsService
    /// </summary>
    public class SettingsServiceTests
    {
        [Fact]
        public void Instance_AlwaysReturnsSameInstance()
        {
            // Act
            var instance1 = SettingsService.Instance;
            var instance2 = SettingsService.Instance;

            // Assert
            instance1.Should().BeSameAs(instance2);
        }

        [Fact]
        public void Settings_AlwaysReturnsNonNull()
        {
            // Arrange
            var service = SettingsService.Instance;

            // Act
            var settings = service.Settings;

            // Assert
            settings.Should().NotBeNull();
        }

        [Fact]
        public void Settings_DefaultValues_AreSet()
        {
            // Arrange
            var service = SettingsService.Instance;

            // Act
            var settings = service.Settings;

            // Assert
            settings.ScriptTimeoutSeconds.Should().BeGreaterThan(0);
            settings.ActivityLogThreshold.Should().BeGreaterThan(0);
        }
    }
}


using System.IO;
using System.Text.Json;
using FluentAssertions;
using IoTPowerShellAgent.Core;
using Xunit;

namespace IoTPowerShellAgent.Tests.Core
{



    public class SettingsServiceTests
    {
        [Fact]
        public void Instance_AlwaysReturnsSameInstance()
        {

            var instance1 = SettingsService.Instance;
            var instance2 = SettingsService.Instance;


            instance1.Should().BeSameAs(instance2);
        }

        [Fact]
        public void Settings_AlwaysReturnsNonNull()
        {

            var service = SettingsService.Instance;


            var settings = service.Settings;


            settings.Should().NotBeNull();
        }

        [Fact]
        public void Settings_DefaultValues_AreSet()
        {

            var service = SettingsService.Instance;


            var settings = service.Settings;


            settings.ScriptTimeoutSeconds.Should().BeGreaterThan(0);
        }
    }
}


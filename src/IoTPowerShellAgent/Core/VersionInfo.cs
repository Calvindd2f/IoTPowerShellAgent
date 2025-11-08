using System;
using System.Reflection;

namespace IoTPowerShellAgent.Core
{
    /// <summary>
    /// Provides version information for the application
    /// </summary>
    public static class VersionInfo
    {
        /// <summary>
        /// Gets the current version of the application
        /// </summary>
        public static string Version
        {
            get
            {
                var assembly = Assembly.GetExecutingAssembly();
                var versionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

                if (versionAttribute != null && !string.IsNullOrEmpty(versionAttribute.InformationalVersion))
                {
                    return versionAttribute.InformationalVersion;
                }

                var fileVersionAttribute = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
                if (fileVersionAttribute != null && !string.IsNullOrEmpty(fileVersionAttribute.Version))
                {
                    return fileVersionAttribute.Version;
                }

                var version = assembly.GetName().Version;
                return version != null ? version.ToString() : "1.0.0";
            }
        }

        /// <summary>
        /// Gets the assembly version
        /// </summary>
        public static Version AssemblyVersion
        {
            get
            {
                var assembly = Assembly.GetExecutingAssembly();
                return assembly.GetName().Version ?? new Version(1, 0, 0);
            }
        }
    }
}


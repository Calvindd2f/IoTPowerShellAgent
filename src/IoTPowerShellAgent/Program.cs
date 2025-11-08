using System;
using System.ServiceProcess;
using System.Threading.Tasks;
using IoTPowerShellAgent.Installation;
using IoTPowerShellAgent.Services;
using IoTPowerShellAgent.TestHelpers;

namespace IoTPowerShellAgent
{
    /// <summary>
    /// Main entry point for the PowerShell Executor Service
    /// </summary>
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static int Main(string[] args)
        {
            // Check for installer commands
            if (args.Length > 0 && IsInstallerCommand(args[0]))
            {
                return InstallerProgram.RunInstaller(args);
            }

            // Check for debug mode
            if (args.Length > 0 && (args[0] == "--debug" || args[0] == "-d"))
            {
                RunDebugMode().GetAwaiter().GetResult();
                return 0;
            }

            // Check for test mode
            if (args.Length > 0 && (args[0] == "--test" || args[0] == "-t"))
            {
                RunTestMode(args).GetAwaiter().GetResult();
                return 0;
            }

            if (Environment.UserInteractive)
            {
                // Run as console application for debugging
                RunAsConsole(args).GetAwaiter().GetResult();
                return 0;
            }
            else
            {
                // Run as Windows Service
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]
                {
                    new PowerShellExecutorService()
                };
                ServiceBase.Run(ServicesToRun);
                return 0;
            }
        }

        private static bool IsInstallerCommand(string command)
        {
            string cmd = command.ToLowerInvariant();
            return cmd == "install" || cmd == "uninstall" ||
                    cmd == "start" || cmd == "stop" || cmd == "status" || cmd == "update";
        }

        private static async Task RunDebugMode()
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║     IoTPowerShellAgent - Interactive Debug Mode              ║");
            Console.WriteLine("║     Test PowerShell execution without IoT Hub connection     ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  <powershell>     Execute PowerShell script");
            Console.WriteLine("  base64:<script>  Execute base64-encoded script");
            Console.WriteLine("  metrics         Show environment metrics");
            Console.WriteLine("  sample          Run sample test script");
            Console.WriteLine("  help            Show this help message");
            Console.WriteLine("  exit, quit      Exit debug mode");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  Get-Date");
            Console.WriteLine("  Get-Process | Select-Object -First 5 Name, CPU");
            Console.WriteLine("  base64:RwBlAHQALQBEAGEAdABlAA==  (Get-Date encoded)");
            Console.WriteLine();

            var tester = new LocalIoTTester();
            bool running = true;

            while (running)
            {
                Console.Write("PS> ");
                string? input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                {
                    continue;
                }

                string command = input.Trim();

                // Handle special commands
                if (command.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                    command.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
                    command.Equals("q", StringComparison.OrdinalIgnoreCase))
                {
                    running = false;
                    Console.WriteLine("Exiting debug mode...");
                    break;
                }

                if (command.Equals("help", StringComparison.OrdinalIgnoreCase) ||
                    command.Equals("?", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine();
                    Console.WriteLine("Commands:");
                    Console.WriteLine("  <powershell>     Execute PowerShell script");
                    Console.WriteLine("  base64:<script>  Execute base64-encoded script");
                    Console.WriteLine("  metrics         Show environment metrics");
                    Console.WriteLine("  sample          Run sample test script");
                    Console.WriteLine("  help            Show this help message");
                    Console.WriteLine("  exit, quit      Exit debug mode");
                    Console.WriteLine();
                    continue;
                }

                if (command.Equals("metrics", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine();
                    await tester.TestEnvironmentMetrics().ConfigureAwait(false);
                    Console.WriteLine();
                    continue;
                }

                if (command.Equals("sample", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine();
                    await tester.TestWithSampleScript().ConfigureAwait(false);
                    Console.WriteLine();
                    continue;
                }

                // Handle base64 prefix
                bool isBase64 = false;
                string script = command;
                if (command.StartsWith("base64:", StringComparison.OrdinalIgnoreCase))
                {
                    script = command.Substring("base64:".Length);
                    isBase64 = true;
                }

                // Execute PowerShell script
                Console.WriteLine();
                try
                {
                    await tester.ExecuteScriptLocally(script, isInlinePowershell: false, isBase64Encoded: isBase64).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                }
                Console.WriteLine();
            }
        }

        private static async Task RunTestMode(string[] args)
        {
            Console.WriteLine("=== IoTPowerShellAgent Local Test Mode ===");
            Console.WriteLine();

            var tester = new LocalIoTTester();

            if (args.Length > 1 && args[1] == "--base64")
            {
                await tester.TestWithBase64Script().ConfigureAwait(false);
            }
            else if (args.Length > 1 && args[1].StartsWith("--script="))
            {
                string script = args[1].Substring("--script=".Length);
                bool isBase64 = args.Length > 2 && args[2] == "--base64";
                await tester.ExecuteScriptLocally(script, isInlinePowershell: false, isBase64Encoded: isBase64).ConfigureAwait(false);
            }
            else if (args.Length > 1 && args[1] == "--metrics")
            {
                await tester.TestEnvironmentMetrics().ConfigureAwait(false);
            }
            else
            {
                await tester.TestWithSampleScript().ConfigureAwait(false);
                Console.WriteLine();
                Console.WriteLine("Usage examples:");
                Console.WriteLine("  dotnet run -- --test                           # Run sample test");
                Console.WriteLine("  dotnet run -- --test --base64                  # Test base64 encoding");
                Console.WriteLine("  dotnet run -- --test --script=\"Get-Date\"       # Test custom script");
                Console.WriteLine("  dotnet run -- --test --script=<base64> --base64 # Test base64 script");
                Console.WriteLine("  dotnet run -- --test --metrics                 # Test environment metrics");
                Console.WriteLine();
                Console.WriteLine("For interactive testing, use:");
                Console.WriteLine("  dotnet run -- --debug                          # Interactive debug mode");
            }
        }

        private static async Task RunAsConsole(string[] args)
        {
            Console.WriteLine("IoTPowerShellAgent - Console Mode");
            Console.WriteLine("Press Ctrl+C to exit...");
            Console.WriteLine();
            Console.WriteLine("Available modes:");
            Console.WriteLine("  --debug, -d     Interactive debug mode (no IoT Hub required)");
            Console.WriteLine("  --test, -t      One-time test execution");
            Console.WriteLine();

            var service = new PowerShellExecutorService();

            try
            {
                // Use reflection to access protected methods for console mode testing
                var onStartMethod = typeof(PowerShellExecutorService).GetMethod("OnStart",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                onStartMethod?.Invoke(service, new object[] { args });

                // Wait for exit
                await Task.Delay(-1);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex}");
            }
            finally
            {
                var onStopMethod = typeof(PowerShellExecutorService).GetMethod("OnStop",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                onStopMethod?.Invoke(service, null);
            }
        }
    }
}

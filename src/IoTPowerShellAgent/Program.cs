using System;
using System.ServiceProcess;
using System.Threading.Tasks;
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
        static void Main(string[] args)
        {
            // Check for test mode
            if (args.Length > 0 && (args[0] == "--test" || args[0] == "-t"))
            {
                RunTestMode(args).Wait();
                return;
            }

            if (Environment.UserInteractive)
            {
                // Run as console application for debugging
                RunAsConsole(args);
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
            }
        }

        private static async Task RunTestMode(string[] args)
        {
            Console.WriteLine("=== IoTPowerShellAgent Local Test Mode ===");
            Console.WriteLine();

            var tester = new LocalIoTTester();

            if (args.Length > 1 && args[1] == "--base64")
            {
                await tester.TestWithBase64Script();
            }
            else if (args.Length > 1 && args[1].StartsWith("--script="))
            {
                string script = args[1].Substring("--script=".Length);
                bool isBase64 = args.Length > 2 && args[2] == "--base64";
                await tester.ExecuteScriptLocally(script, isInlinePowershell: false, isBase64Encoded: isBase64);
            }
            else
            {
                await tester.TestWithSampleScript();
                Console.WriteLine();
                Console.WriteLine("Usage examples:");
                Console.WriteLine("  dotnet run -- --test                           # Run sample test");
                Console.WriteLine("  dotnet run -- --test --base64                  # Test base64 encoding");
                Console.WriteLine("  dotnet run -- --test --script=\"Get-Date\"       # Test custom script");
                Console.WriteLine("  dotnet run -- --test --script=<base64> --base64 # Test base64 script");
            }
        }

        private static async void RunAsConsole(string[] args)
        {
            Console.WriteLine("IoTPowerShellAgent - Console Mode");
            Console.WriteLine("Press Ctrl+C to exit...");
            Console.WriteLine("Use '--test' argument to run in test mode");

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

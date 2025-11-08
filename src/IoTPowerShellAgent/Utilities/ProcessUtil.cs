using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace IoTPowerShellAgent.Utilities
{
    /// <summary>
    /// Utility class for process management using P/Invoke for better performance
    /// </summary>
    public static class ProcessUtil
    {
        private static long _lastCpuTime = 0;
        private static DateTime _lastCpuSample = DateTime.MinValue;
        private static long _lastSystemTime = 0;

        /// <summary>
        /// Gets the current process information
        /// </summary>
        public static Process GetCurrentProcess()
        {
            return Process.GetCurrentProcess();
        }

        /// <summary>
        /// Gets process memory usage in MB using P/Invoke for better performance
        /// </summary>
        public static long GetMemoryUsageMB()
        {
            try
            {
                IntPtr processHandle = WindowsApiInterop.GetCurrentProcess();
                WindowsApiInterop.PROCESS_MEMORY_COUNTERS_EX memCounters = new WindowsApiInterop.PROCESS_MEMORY_COUNTERS_EX();
                memCounters.cb = (uint)Marshal.SizeOf(typeof(WindowsApiInterop.PROCESS_MEMORY_COUNTERS_EX));

                if (WindowsApiInterop.GetProcessMemoryInfo(processHandle, out memCounters, memCounters.cb))
                {
                    return (long)(memCounters.WorkingSetSize.ToUInt64() / (1024 * 1024));
                }
            }
            catch
            {
                // Fallback to managed API
            }

            // Fallback to managed API if P/Invoke fails
            using (var process = Process.GetCurrentProcess())
            {
                return process.WorkingSet64 / (1024 * 1024);
            }
        }

        /// <summary>
        /// Gets CPU usage percentage using P/Invoke for accurate measurement
        /// </summary>
        public static double GetCpuUsage()
        {
            try
            {
                IntPtr processHandle = WindowsApiInterop.GetCurrentProcess();

                if (!WindowsApiInterop.GetProcessTimes(processHandle,
                    out var creationTime,
                    out var exitTime,
                    out var kernelTime,
                    out var userTime))
                {
                    return 0.0;
                }

                if (!WindowsApiInterop.GetSystemTimes(
                    out var idleTime,
                    out var systemKernelTime,
                    out var systemUserTime))
                {
                    return 0.0;
                }

                long currentProcessTime = WindowsApiInterop.FileTimeToLong(kernelTime) +
                                        WindowsApiInterop.FileTimeToLong(userTime);
                long currentSystemTime = WindowsApiInterop.FileTimeToLong(systemKernelTime) +
                                        WindowsApiInterop.FileTimeToLong(systemUserTime);

                DateTime now = DateTime.UtcNow;

                if (_lastCpuSample != DateTime.MinValue)
                {
                    long processTimeDiff = currentProcessTime - _lastCpuTime;
                    long systemTimeDiff = currentSystemTime - _lastSystemTime;
                    double timeDiffSeconds = (now - _lastCpuSample).TotalSeconds;

                    if (systemTimeDiff > 0 && timeDiffSeconds > 0)
                    {
                        double cpuPercent = (100.0 * processTimeDiff) / systemTimeDiff;
                        _lastCpuTime = currentProcessTime;
                        _lastSystemTime = currentSystemTime;
                        _lastCpuSample = now;
                        return cpuPercent;
                    }
                }

                _lastCpuTime = currentProcessTime;
                _lastSystemTime = currentSystemTime;
                _lastCpuSample = now;
                return 0.0;
            }
            catch
            {
                // Fallback to managed API if P/Invoke fails
                try
                {
                    using (var process = Process.GetCurrentProcess())
                    {
                        return process.TotalProcessorTime.TotalMilliseconds / Environment.TickCount;
                    }
                }
                catch
                {
                    return 0.0;
                }
            }
        }

        /// <summary>
        /// Gets detailed memory information using P/Invoke
        /// </summary>
        public static (long WorkingSetMB, long PrivateMB, long PeakWorkingSetMB) GetDetailedMemoryInfo()
        {
            try
            {
                IntPtr processHandle = WindowsApiInterop.GetCurrentProcess();
                WindowsApiInterop.PROCESS_MEMORY_COUNTERS_EX memCounters = new WindowsApiInterop.PROCESS_MEMORY_COUNTERS_EX();
                memCounters.cb = (uint)Marshal.SizeOf(typeof(WindowsApiInterop.PROCESS_MEMORY_COUNTERS_EX));

                if (WindowsApiInterop.GetProcessMemoryInfo(processHandle, out memCounters, memCounters.cb))
                {
                    return (
                        (long)(memCounters.WorkingSetSize.ToUInt64() / (1024 * 1024)),
                        (long)(memCounters.PrivateUsage.ToUInt64() / (1024 * 1024)),
                        (long)(memCounters.PeakWorkingSetSize.ToUInt64() / (1024 * 1024))
                    );
                }
            }
            catch
            {
                // Fallback handling
            }

            // Fallback
            using (var process = Process.GetCurrentProcess())
            {
                return (
                    process.WorkingSet64 / (1024 * 1024),
                    process.PrivateMemorySize64 / (1024 * 1024),
                    process.PeakWorkingSet64 / (1024 * 1024)
                );
            }
        }

        /// <summary>
        /// Sets process priority class using P/Invoke
        /// </summary>
        public static bool SetProcessPriority(uint priorityClass)
        {
            try
            {
                IntPtr processHandle = WindowsApiInterop.GetCurrentProcess();
                return WindowsApiInterop.SetPriorityClass(processHandle, priorityClass);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Sets thread priority using P/Invoke
        /// </summary>
        public static bool SetThreadPriority(int priority)
        {
            try
            {
                IntPtr threadHandle = WindowsApiInterop.GetCurrentThread();
                return WindowsApiInterop.SetThreadPriority(threadHandle, priority);
            }
            catch
            {
                return false;
            }
        }
    }
}

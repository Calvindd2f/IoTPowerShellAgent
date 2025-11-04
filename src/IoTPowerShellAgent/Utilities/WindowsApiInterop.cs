using System;
using System.Runtime.InteropServices;

namespace IoTPowerShellAgent.Utilities
{
    /// <summary>
    /// Windows API interop declarations for performance-critical operations
    /// </summary>
    public static class WindowsApiInterop
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetProcessTimes(
            IntPtr hProcess,
            out System.Runtime.InteropServices.ComTypes.FILETIME lpCreationTime,
            out System.Runtime.InteropServices.ComTypes.FILETIME lpExitTime,
            out System.Runtime.InteropServices.ComTypes.FILETIME lpKernelTime,
            out System.Runtime.InteropServices.ComTypes.FILETIME lpUserTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetSystemTimes(
            out System.Runtime.InteropServices.ComTypes.FILETIME lpIdleTime,
            out System.Runtime.InteropServices.ComTypes.FILETIME lpKernelTime,
            out System.Runtime.InteropServices.ComTypes.FILETIME lpUserTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool QueryPerformanceFrequency(out long lpFrequency);

        [DllImport("psapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetProcessMemoryInfo(
            IntPtr hProcess,
            out PROCESS_MEMORY_COUNTERS_EX ppsmemCounters,
            uint cb);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint GetLastError();

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_MEMORY_COUNTERS_EX
        {
            public uint cb;
            public uint PageFaultCount;
            public UIntPtr PeakWorkingSetSize;
            public UIntPtr WorkingSetSize;
            public UIntPtr QuotaPeakPagedPoolUsage;
            public UIntPtr QuotaPagedPoolUsage;
            public UIntPtr QuotaPeakNonPagedPoolUsage;
            public UIntPtr QuotaNonPagedPoolUsage;
            public UIntPtr PagefileUsage;
            public UIntPtr PeakPagefileUsage;
            public UIntPtr PrivateUsage;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetPriorityClass(IntPtr hProcess, uint dwPriorityClass);

        public const uint HIGH_PRIORITY_CLASS = 0x00000080;
        public const uint ABOVE_NORMAL_PRIORITY_CLASS = 0x00008000;
        public const uint NORMAL_PRIORITY_CLASS = 0x00000020;
        public const uint BELOW_NORMAL_PRIORITY_CLASS = 0x00004000;
        public const uint IDLE_PRIORITY_CLASS = 0x00000040;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint GetCurrentThreadId();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetThreadPriority(IntPtr hThread, int nPriority);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetCurrentThread();

        public const int THREAD_PRIORITY_HIGHEST = 2;
        public const int THREAD_PRIORITY_ABOVE_NORMAL = 1;
        public const int THREAD_PRIORITY_NORMAL = 0;
        public const int THREAD_PRIORITY_BELOW_NORMAL = -1;
        public const int THREAD_PRIORITY_LOWEST = -2;

        /// <summary>
        /// Converts FILETIME to long ticks
        /// </summary>
        public static long FileTimeToLong(System.Runtime.InteropServices.ComTypes.FILETIME fileTime)
        {
            return ((long)fileTime.dwHighDateTime << 32) + fileTime.dwLowDateTime;
        }

        /// <summary>
        /// Converts FILETIME to TimeSpan
        /// </summary>
        public static TimeSpan FileTimeToTimeSpan(System.Runtime.InteropServices.ComTypes.FILETIME fileTime)
        {
            long ticks = FileTimeToLong(fileTime);
            return TimeSpan.FromTicks(ticks);
        }
    }
}

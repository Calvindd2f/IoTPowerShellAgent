using System;
using System.Runtime.InteropServices;

namespace IoTPowerShellAgent.Utilities
{



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

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetCurrentThread();

        public const int THREAD_PRIORITY_HIGHEST = 2;
        public const int THREAD_PRIORITY_ABOVE_NORMAL = 1;
        public const int THREAD_PRIORITY_NORMAL = 0;
        public const int THREAD_PRIORITY_BELOW_NORMAL = -1;
        public const int THREAD_PRIORITY_LOWEST = -2;


        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetSystemTimePreciseAsFileTime(out System.Runtime.InteropServices.ComTypes.FILETIME lpSystemTimeAsFileTime);


        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int RegOpenKeyEx(
            IntPtr hKey,
            string lpSubKey,
            uint ulOptions,
            int samDesired,
            out IntPtr phkResult);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern int RegQueryValueEx(
            IntPtr hKey,
            string lpValueName,
            IntPtr lpReserved,
            out uint lpType,
            IntPtr lpData,
            ref uint lpcbData);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int RegQueryValueEx(
            IntPtr hKey,
            string lpValueName,
            IntPtr lpReserved,
            out uint lpType,
            [Out] byte[] lpData,
            ref uint lpcbData);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RegCloseKey(IntPtr hKey);

        public const int KEY_QUERY_VALUE = 0x0001;
        public const int KEY_READ = 0x20019;
        public static readonly IntPtr HKEY_LOCAL_MACHINE = new IntPtr(unchecked((int)0x80000002));


        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ReportEvent(
            IntPtr hEventLog,
            ushort wType,
            ushort wCategory,
            uint dwEventID,
            IntPtr lpUserSid,
            ushort wNumStrings,
            uint dwDataSize,
            string[] lpStrings,
            IntPtr lpRawData);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr RegisterEventSource(string lpUNCServerName, string lpSourceName);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeregisterEventSource(IntPtr hEventLog);

        public const ushort EVENTLOG_SUCCESS = 0x0000;
        public const ushort EVENTLOG_ERROR_TYPE = 0x0001;
        public const ushort EVENTLOG_WARNING_TYPE = 0x0002;
        public const ushort EVENTLOG_INFORMATION_TYPE = 0x0004;


        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern uint GetEnvironmentVariable(string lpName, System.Text.StringBuilder lpBuffer, uint nSize);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetEnvironmentVariable(string lpName, string? lpValue);




        public static long FileTimeToLong(System.Runtime.InteropServices.ComTypes.FILETIME fileTime)
        {
            return ((long)fileTime.dwHighDateTime << 32) + fileTime.dwLowDateTime;
        }




        public static TimeSpan FileTimeToTimeSpan(System.Runtime.InteropServices.ComTypes.FILETIME fileTime)
        {
            long ticks = FileTimeToLong(fileTime);
            return TimeSpan.FromTicks(ticks);
        }





        public static DateTime GetHighResolutionTimestamp()
        {
            try
            {
                if (GetSystemTimePreciseAsFileTime(out var fileTime))
                {
                    long ticks = FileTimeToLong(fileTime);


                    const long FileTimeOffset = 504911232000000000L;
                    return new DateTime(ticks + FileTimeOffset, DateTimeKind.Utc);
                }
            }
            catch
            {

            }
            return DateTime.UtcNow;
        }




        public static string? GetEnvironmentVariableNative(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;


            uint size = GetEnvironmentVariable(name, null!, 0);
            if (size == 0)
            {
                uint error = GetLastError();
                if (error == 203)
                    return null;
                return null;
            }


            var buffer = new System.Text.StringBuilder((int)size);
            uint result = GetEnvironmentVariable(name, buffer, size);
            if (result == 0)
                return null;

            return buffer.ToString();
        }
    }
}

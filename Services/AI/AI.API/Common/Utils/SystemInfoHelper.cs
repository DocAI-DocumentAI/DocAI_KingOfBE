using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AI.API.Common.Utils
{
    public static class SystemInfoHelper
    {
        public static SystemInfo GetSystemInfo()
        {
            return new SystemInfo
            {
                MachineName = Environment.MachineName,
                OSVersion = Environment.OSVersion.ToString(),
                ProcessorCount = Environment.ProcessorCount,
                WorkingSet = Environment.WorkingSet,
                TotalMemory = GC.GetTotalMemory(false),
                RuntimeVersion = RuntimeInformation.FrameworkDescription,
                ApplicationVersion = GetApplicationVersion(),
                Uptime = GetUptime(),
                ProcessId = Environment.ProcessId,
                ThreadCount = Process.GetCurrentProcess().Threads.Count,
                HandleCount = Process.GetCurrentProcess().HandleCount
            };
        }

        public static string GetApplicationVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version?.ToString() ?? "Unknown";
        }

        public static TimeSpan GetUptime()
        {
            return TimeSpan.FromMilliseconds(Environment.TickCount64);
        }

        public static MemoryInfo GetMemoryInfo()
        {
            var process = Process.GetCurrentProcess();
            return new MemoryInfo
            {
                WorkingSet = process.WorkingSet64,
                PrivateMemorySize = process.PrivateMemorySize64,
                VirtualMemorySize = process.VirtualMemorySize64,
                PagedMemorySize = process.PagedMemorySize64,
                NonpagedSystemMemorySize = process.NonpagedSystemMemorySize64,
                PagedSystemMemorySize = process.PagedSystemMemorySize64,
                GCTotalMemory = GC.GetTotalMemory(false),
                GCGen0Collections = GC.CollectionCount(0),
                GCGen1Collections = GC.CollectionCount(1),
                GCGen2Collections = GC.CollectionCount(2)
            };
        }

        public static ProcessInfo GetProcessInfo()
        {
            var process = Process.GetCurrentProcess();
            return new ProcessInfo
            {
                ProcessId = process.Id,
                ProcessName = process.ProcessName,
                StartTime = process.StartTime,
                TotalProcessorTime = process.TotalProcessorTime,
                UserProcessorTime = process.UserProcessorTime,
                PrivilegedProcessorTime = process.PrivilegedProcessorTime,
                ThreadCount = process.Threads.Count,
                HandleCount = process.HandleCount,
                BasePriority = process.BasePriority,
                PriorityClass = process.PriorityClass.ToString()
            };
        }
    }

    public class SystemInfo
    {
        public string MachineName { get; set; } = string.Empty;
        public string OSVersion { get; set; } = string.Empty;
        public int ProcessorCount { get; set; }
        public long WorkingSet { get; set; }
        public long TotalMemory { get; set; }
        public string RuntimeVersion { get; set; } = string.Empty;
        public string ApplicationVersion { get; set; } = string.Empty;
        public TimeSpan Uptime { get; set; }
        public int ProcessId { get; set; }
        public int ThreadCount { get; set; }
        public long HandleCount { get; set; }
    }

    public class MemoryInfo
    {
        public long WorkingSet { get; set; }
        public long PrivateMemorySize { get; set; }
        public long VirtualMemorySize { get; set; }
        public long PagedMemorySize { get; set; }
        public long NonpagedSystemMemorySize { get; set; }
        public long PagedSystemMemorySize { get; set; }
        public long GCTotalMemory { get; set; }
        public int GCGen0Collections { get; set; }
        public int GCGen1Collections { get; set; }
        public int GCGen2Collections { get; set; }
    }

    public class ProcessInfo
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public TimeSpan TotalProcessorTime { get; set; }
        public TimeSpan UserProcessorTime { get; set; }
        public TimeSpan PrivilegedProcessorTime { get; set; }
        public int ThreadCount { get; set; }
        public long HandleCount { get; set; }
        public int BasePriority { get; set; }
        public string PriorityClass { get; set; } = string.Empty;
    }
}

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DynaDocs.Tests.Services;

internal static class ThreadCpuTime
{
    // Linux's calling-thread clock excludes other work on the shared host. Other test hosts retain
    // the wall clock. GC work stays in the ratio: subtracting a wall-clock pause from thread CPU
    // can make elapsed time negative when the thread is descheduled during collection.
    public static TimeSpan Now()
    {
        if (!OperatingSystem.IsLinux())
            return Stopwatch.GetElapsedTime(0);
        Assert.Equal(0, clock_gettime(3, out var time));
        return TimeSpan.FromSeconds(time.Seconds) + TimeSpan.FromTicks(time.Nanoseconds / 100);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Timespec
    {
        public long Seconds;
        public long Nanoseconds;
    }

    [DllImport("libc")]
    private static extern int clock_gettime(int clock, out Timespec time);
}

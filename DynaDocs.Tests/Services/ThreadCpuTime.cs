using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DynaDocs.Tests.Services;

internal static class ThreadCpuTime
{
    // Linux's calling-thread clock excludes other work on the shared host. Other test hosts retain
    // the wall clock. Blocking GC pauses are excluded from the CPU ratio, not the absolute wall bound.
    public static TimeSpan Now()
    {
        if (!OperatingSystem.IsLinux())
            return Stopwatch.GetElapsedTime(0);
        Assert.Equal(0, clock_gettime(3, out var time));
        return TimeSpan.FromSeconds(time.Seconds) + TimeSpan.FromTicks(time.Nanoseconds / 100)
            - GC.GetTotalPauseDuration();
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

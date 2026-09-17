using System.Diagnostics;

if (args.Length == 1 && args[0] == "child")
{
    Func<int, int> executed = value => value > 0 ? 10 : 20; Func<int, int> neverInvoked = value => value > 0 ? 30 : 40;
    var result = executed(1);
    GC.KeepAlive(neverInvoked);
    if (result != 10)
    {
        throw new InvalidOperationException($"Child lambda failed: {result}");
    }

    Console.WriteLine("CHILD_ASSERT=1");
    return;
}

var start = new ProcessStartInfo(Environment.ProcessPath!);
start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "SameLineChildProbe.dll"));
start.ArgumentList.Add("child");
start.UseShellExecute = false;
using var child = Process.Start(start) ?? throw new InvalidOperationException("Child did not start");
child.WaitForExit();
if (child.ExitCode != 0)
{
    throw new InvalidOperationException($"Child failed: {child.ExitCode}");
}

Console.WriteLine("PARENT_ASSERT=1");

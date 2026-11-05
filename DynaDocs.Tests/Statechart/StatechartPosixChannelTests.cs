namespace DynaDocs.Tests.Statechart;

using System.Diagnostics;
using System.Text;
using DynaDocs.Services.Statechart;

public class StatechartPosixChannelTests
{
    private const int SunPath = 108; // Linux sockaddr_un.sun_path, NUL included.

    // The connector is a separate dydo process; it finds the socket only through the TMPDIR it inherits.
    [SkippableFact]
    public async Task AChildInheritingALongTempDirConnectsAndLeavesNoSocketBehind()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), "The POSIX statechart channel runs on Linux.");
        using var scope = new StatechartTestDirectory();
        var temp = Directory.CreateDirectory(Path.Combine(scope.Path, new string('t', 80))).FullName;
        var name = "statechart-" + Guid.NewGuid().ToString("N");
        Assert.True(Bytes(Path.Combine(temp, "CoreFxPipe_" + name)) > SunPath);
        var nonce = Guid.NewGuid().ToString("N");
        var start = StatechartTestBinary.Start();
        foreach (var argument in new[] { "__statechart-posix-anchor", name, nonce }) start.ArgumentList.Add(argument);
        start.Environment["TMPDIR"] = temp;

        using var server = PosixStatechartChannel.Listen(name, temp);
        var socket = PosixStatechartSocket.For(name, temp);
        Assert.True(File.Exists(socket));
        using var child = Process.Start(start)!;
        await server.AcceptAsync();
        Assert.False(File.Exists(socket));
        await server.ExpectAsync("anchor:1:" + nonce + ":" + child.Id);
        await server.SendAsync("stop");
        await child.WaitForExitAsync();
    }

    [SkippableFact]
    public void ASocketPathFitsSunPathCountedInUtf8Bytes()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), "The POSIX statechart channel runs on Linux.");
        var name = "statechart-" + Guid.NewGuid().ToString("N");
        // The old path fits in characters, not in bytes.
        var temp = "/" + new string('é', 40);
        Assert.True(Path.Combine(temp, "CoreFxPipe_" + name).Length + 1 <= SunPath);
        Assert.True(Bytes(Path.Combine(temp, "CoreFxPipe_" + name)) > SunPath);
        Assert.True(Bytes(PosixStatechartSocket.For(name, temp)) <= SunPath);
    }

    private static int Bytes(string path) => Encoding.UTF8.GetByteCount(path) + 1;
}

namespace DynaDocs.Services.Statechart;

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

internal sealed class PosixStatechartProcess
{
    public int Id { get; }
    public bool Confirmed => _groupReady;
    private bool _groupReady;
    private bool _drained;
    private bool _reaped;

    private PosixStatechartProcess(int id) => Id = id;

    public static PosixStatechartProcess Start(string file, IEnumerable<string> arguments,
        IEnumerable<string> environment)
    {
        RequirePosix();
        using var argv = new PosixStatechartArguments(new[] { file }.Concat(arguments));
        using var env = new PosixStatechartArguments(environment);
        // Process.Start's reaper cannot own this child: its unreaped PID is the group identity.
        var error = PosixStatechartNative.Spawn(out var pid, file, 0, 0, argv.Pointer, env.Pointer);
        if (error != 0) throw new Win32Exception(error, "Cannot start statechart lifetime owner.");
        return new PosixStatechartProcess(pid);
    }

    // Replaces this process; it returns only when exec failed, with the error for the caller to throw.
    public static Win32Exception Handoff(string file, IEnumerable<string> arguments, IEnumerable<string> environment)
    {
        RequirePosix();
        using var argv = new PosixStatechartArguments(new[] { file }.Concat(arguments));
        using var env = new PosixStatechartArguments(environment);
        PosixStatechartNative.Exec(file, argv.Pointer, env.Pointer);
        return new Win32Exception(Marshal.GetLastPInvokeError(), "Statechart exec handoff failed.");
    }

    public static void BecomeGroupLeader()
    {
        RequirePosix();
        if (PosixStatechartNative.SetGroup(0, 0) != 0) throw new Win32Exception();
    }

    public static void JoinGroup(int group)
    {
        RequirePosix();
        if (group <= 0 || PosixStatechartNative.SetGroup(0, group) != 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Cannot join owned statechart group.");
    }

    public static void RequireGroup(int pid, int group)
    {
        RequirePosix();
        if (group <= 0 || PosixStatechartNative.GetGroup(pid) != group)
            throw new IOException("Checking child did not enter the acknowledged statechart group.");
    }

    public static void StopCurrentGroup()
    {
        RequirePosix();
        if (PosixStatechartNative.Kill(0, 9) != 0) throw new Win32Exception();
    }

    public void ConfirmGroup(int acknowledgment)
    {
        RequireChild();
        if (_groupReady || acknowledgment != Id || PosixStatechartNative.GetGroup(Id) != Id)
            throw new InvalidDataException("Statechart group leader acknowledgment does not match its child.");
        _groupReady = true;
    }

    public void WaitWithoutReaping()
    {
        RequireChild();
        var information = Marshal.AllocHGlobal(128);
        try
        {
            // siginfo_t is opaque here. Only waitid's return is consumed; no guessed field offsets.
            var options = 4 | (OperatingSystem.IsMacOS() ? 0x20 : 0x1000000); // WEXITED | WNOWAIT
            if (PosixStatechartNative.WaitId(1, (uint)Id, information, options) != 0)
                throw new Win32Exception();
        }
        finally
        {
            Marshal.FreeHGlobal(information);
        }
    }

    public void Stop()
    {
        RequireChild();
        var target = _groupReady ? -Id : Id;
        if (PosixStatechartNative.Kill(target, 9) != 0 && Marshal.GetLastPInvokeError() != 3)
            throw new Win32Exception();
    }

    // Returns once the group is empty; a group still live at the deadline cancels instead.
    public async Task WaitForEmptyAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        RequireChild();
        if (!_groupReady) throw new InvalidOperationException("Statechart group is not registered.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        while (await HasLiveMembersAsync(deadline.Token)) await Task.Delay(10, deadline.Token);
        _drained = true;
    }

    public int Reap()
    {
        RequireChild();
        if (_groupReady && !_drained)
            throw new InvalidOperationException("Cannot release statechart group identity before drain.");
        if (PosixStatechartNative.WaitPid(Id, out var status, 0) != Id) throw new Win32Exception();
        _reaped = true;
        return status;
    }

    private async Task<bool> HasLiveMembersAsync(CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("/bin/ps")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("-axo");
        start.ArgumentList.Add("pgid=,stat=");
        using var process = Process.Start(start) ?? throw new IOException("Cannot inspect statechart group.");
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0) throw new IOException("Statechart group inspection failed: " + error);
        return ListsLiveMember(output, Id);
    }

    internal static bool ListsLiveMember(string listing, int group) =>
        listing.Split('\n', StringSplitOptions.RemoveEmptyEntries).Any(line => IsLiveMember(line, group));

    // procps prints the pgid unsigned. A task the kernel releases mid-scan reads pgid -1, listed as 4294967295: no member.
    private static bool IsLiveMember(string line, int group)
    {
        var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 2 || !uint.TryParse(fields[0], out var listed))
            throw new InvalidDataException("Cannot interpret statechart group inspection.");
        return listed == group && !fields[1].StartsWith('Z');
    }

    private void RequireChild()
    {
        if (_reaped) throw new InvalidOperationException("Statechart child identity was already released.");
    }

    private static void RequirePosix()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException();
    }
}

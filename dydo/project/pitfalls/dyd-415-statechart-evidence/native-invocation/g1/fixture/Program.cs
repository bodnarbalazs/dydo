using System.ComponentModel;
using System.Collections;
using System.Diagnostics;
using System.Net.Sockets;
using DynaDocs.Services.Statechart;

try
{
    return args[0] switch
    {
        "error" => NativeError(int.Parse(args[1])),
        "owner" => await Owner(args[1]),
        "cli" => await Cli(),
        "worker" => await Worker(),
        "leaf" => await Leaf(),
        "observer" => await Observer(args[1]),
        "posix-exec" => Handoff(),
        "posix-exec-wait" => HandoffWait(),
        "posix-owner" => await PosixOwner(args[1]),
        "posix-anchor" => await PosixAnchor(args[1]),
        "posix-worker" => await PosixWorker(),
        "posix-leaf" => await PosixLeaf(),
        _ => throw new ArgumentException("Unknown native fixture role.")
    };
}
catch (InvalidDataException error)
{
    Console.Error.WriteLine(error.Message);
    return 2;
}

static Process Start(string role, params string[] arguments)
{
    var start = new ProcessStartInfo(Environment.ProcessPath!)
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = false
    };
    start.ArgumentList.Add(role);
    foreach (var argument in arguments) start.ArgumentList.Add(argument);
    return Process.Start(start) ?? throw new InvalidOperationException("Fixture child did not start.");
}

static async Task Say(string value)
{
    await Console.Out.WriteLineAsync(value);
    await Console.Out.FlushAsync();
}

static int NativeError(int pid)
{
    using var job = new WindowsStatechartJob();
    try
    {
        job.Attach(pid);
        return 3;
    }
    catch (Win32Exception error)
    {
        Console.WriteLine(error.NativeErrorCode);
        return 0;
    }
}

static async Task<int> Owner(string receipt)
{
    using var job = new WindowsStatechartJob();
    using var observer = Start("observer", receipt);
    await observer.StandardInput.WriteLineAsync(job.DuplicateCompletionPortTo(observer).ToString());
    await observer.StandardInput.FlushAsync();
    if (await observer.StandardOutput.ReadLineAsync() != "observer-ready")
        throw new InvalidDataException("Independent observer did not become ready.");
    using var cli = Start("cli");
    var gate = new StatechartProtocol();
    gate.AcceptReady(await cli.StandardOutput.ReadLineAsync());
    await Say($"gated {Environment.ProcessId} {observer.Id} {cli.Id}");
    if (await Console.In.ReadLineAsync() != "attach") return 2;
    job.Attach(cli.Id);
    gate.ConfirmOwnership();
    await Say($"owned {job.Contains(cli.Id)} {job.Contains(observer.Id)}");
    if (await Console.In.ReadLineAsync() != "register") return 2;
    // The fixture acknowledges the gate only; it does not claim production directory registration.
    gate.ConfirmScratchRegistration();
    await Say("registered");
    if (await Console.In.ReadLineAsync() != "go") return 2;
    await gate.ReleaseAsync(cli.StandardInput);
    await Say(await cli.StandardOutput.ReadLineAsync() ?? "missing-tree");
    if (await Console.In.ReadLineAsync() != "terminate") return 2;
    job.Terminate();
    await Say("terminated");
    if (await Console.In.ReadLineAsync() != "count") return 2;
    await Say($"active {job.ActiveProcesses()}");
    observer.StandardInput.Close();
    await observer.WaitForExitAsync();
    return 0;
}

static async Task<int> Cli()
{
    await StatechartProtocol.WaitForGoAsync(Console.In, Console.Out);
    using var worker = Start("worker");
    var gate = new StatechartProtocol();
    gate.AcceptReady(await worker.StandardOutput.ReadLineAsync());
    gate.ConfirmOwnership();
    gate.ConfirmScratchRegistration();
    await gate.ReleaseAsync(worker.StandardInput);
    await Say($"tree {Environment.ProcessId} {await worker.StandardOutput.ReadLineAsync()}");
    await Console.In.ReadToEndAsync();
    return 2;
}

static async Task<int> Worker()
{
    await StatechartProtocol.WaitForGoAsync(Console.In, Console.Out);
    using var leaf = Start("leaf");
    if (await leaf.StandardOutput.ReadLineAsync() != "leaf-ready")
        throw new InvalidDataException("Descendant did not become ready.");
    await Say($"{Environment.ProcessId} {leaf.Id}");
    await Console.In.ReadToEndAsync();
    return 2;
}

static async Task<int> Leaf()
{
    await Say("leaf-ready");
    await Console.In.ReadToEndAsync();
    return 2;
}

static async Task<int> Observer(string receipt)
{
    var raw = await Console.In.ReadLineAsync();
    if (!nint.TryParse(raw, out var handle)) return 2;
    using var completion = new WindowsStatechartCompletion(handle);
    await Say("observer-ready");
    await Console.In.ReadToEndAsync();
    var empty = completion.WaitForEmpty(TimeSpan.FromSeconds(3));
    // Notification is an observation, never permission to delete directories in this feasibility fixture.
    await File.WriteAllTextAsync(receipt, empty ? "empty-observed\n" : "ambiguous-retained\n");
    return empty ? 0 : 2;
}

static int Handoff()
{
    PosixStatechartProcess.Handoff("/bin/sh", ["-c",
        "printf '%s|%s|%s\\n' \"$$\" \"$STATECHART_PROBE_TEXT\" \"$1\"; exit 17",
        "sh", "argument with spaces é"], ["STATECHART_PROBE_TEXT=environment with spaces é"]);
    return 2;
}

static IEnumerable<string> CurrentEnvironment() => Environment.GetEnvironmentVariables()
    .Cast<DictionaryEntry>().Select(value => $"{value.Key}={value.Value}");

static int HandoffWait()
{
    PosixStatechartProcess.Handoff("/bin/sh", ["-c", "printf 'exec-ready\\n'; read -r token"], []);
    return 2;
}

static async Task<int> PosixOwner(string receipt)
{
    var address = receipt + ".socket";
    using var server = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
    server.Bind(new UnixDomainSocketEndPoint(address));
    server.Listen(1);
    var child = PosixStatechartProcess.Start(Environment.ProcessPath!, ["posix-anchor", address],
        CurrentEnvironment());
    using var connection = await server.AcceptAsync();
    using var stream = new NetworkStream(connection, ownsSocket: false);
    using var reader = new StreamReader(stream, leaveOpen: true);
    using var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };
    var group = await reader.ReadLineAsync();
    child.ConfirmGroup(int.Parse(group ?? "0"));
    var gate = new StatechartProtocol();
    gate.AcceptReady(await reader.ReadLineAsync());
    gate.ConfirmOwnership();
    gate.ConfirmScratchRegistration();
    await Say($"gated {Environment.ProcessId} {child.Id}");
    var exited = Task.Run(child.WaitWithoutReaping);
    if (await Console.In.ReadLineAsync() != "go") return 2;
    await gate.ReleaseAsync(writer);
    await Say(await reader.ReadLineAsync() ?? "missing-tree");
    var input = Task.Run(Console.In.ReadLine);
    var next = await Task.WhenAny(input, exited);
    if (next == input && await input == "exit-worker")
    {
        await writer.WriteLineAsync("exit-worker");
        await Say(await reader.ReadLineAsync() ?? "missing-worker-exit");
        next = await Task.WhenAny(Task.Run(Console.In.ReadLine), exited);
    }
    connection.Shutdown(SocketShutdown.Both);
    await exited;
    // The waitable child still pins its PID even when the anchor was killed first.
    child.Stop();
    await child.WaitForEmptyAsync(TimeSpan.FromSeconds(5));
    var status = child.Reap();
    var refused = false;
    try { child.Stop(); }
    catch (InvalidOperationException) { refused = true; }
    await File.WriteAllTextAsync(receipt, $"drained status={status} replayRefused={refused}\n");
    return refused ? 0 : 2;
}

static async Task<int> PosixAnchor(string address)
{
    PosixStatechartProcess.BecomeGroupLeader();
    using var connection = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
    await connection.ConnectAsync(new UnixDomainSocketEndPoint(address));
    using var stream = new NetworkStream(connection, ownsSocket: false);
    using var reader = new StreamReader(stream, leaveOpen: true);
    using var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };
    await writer.WriteLineAsync(Environment.ProcessId.ToString());
    await StatechartProtocol.WaitForGoAsync(reader, writer);
    using var worker = Start("posix-worker");
    if (await worker.StandardOutput.ReadLineAsync() is not { } descendants)
        throw new InvalidDataException("POSIX worker did not become ready.");
    await writer.WriteLineAsync($"tree {Environment.ProcessId} {descendants}");
    if (await reader.ReadLineAsync() == "exit-worker")
    {
        worker.Kill();
        await worker.WaitForExitAsync();
        await writer.WriteLineAsync("worker-exited");
        await reader.ReadToEndAsync();
    }
    // EOF is a lifetime event; this idle leader kills its own group, without a stale PID lookup.
    PosixStatechartProcess.StopCurrentGroup();
    return 2;
}

static async Task<int> PosixWorker()
{
    using var leaf = Start("posix-leaf");
    if (await leaf.StandardOutput.ReadLineAsync() != "leaf-ready")
        throw new InvalidDataException("POSIX descendant did not become ready.");
    await Say($"{Environment.ProcessId} {leaf.Id}");
    await Console.In.ReadToEndAsync();
    return 2;
}

static async Task<int> PosixLeaf()
{
    await Say("leaf-ready");
    using var idle = new ManualResetEvent(false);
    idle.WaitOne();
    return 2;
}

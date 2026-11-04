namespace DynaDocs.Services.Statechart;

using System.Runtime.InteropServices;
using System.Security.Cryptography;

internal sealed class PosixStatechartOwner
{
    private readonly PosixStatechartChannel _channel;
    private readonly PosixStatechartAnchor _anchor;
    private readonly string _nonce;
    private readonly string[] _arguments;
    private readonly Task _outerLost;
    private StatechartScratch? _stage;
    private StatechartScratch? _temp;
    private bool _drained;
    private bool _completed;
    private bool _handedOff;
    private StatechartRequests? _requests;
    private StatechartRequest? _request;
    private string? _requestPath;
    private string? _requestDigest;
    private bool _resultRecorded;

    private PosixStatechartOwner(PosixStatechartChannel channel, PosixStatechartAnchor anchor,
        string[] arguments, Task outerLost)
    {
        _channel = channel;
        _anchor = anchor;
        _arguments = arguments;
        _nonce = arguments[1];
        _outerLost = outerLost;
    }

    public static async Task<int> RunAsync(string[] arguments)
    {
        if (arguments.Length != 5 || !Guid.TryParseExact(arguments[1], "N", out _))
            throw new InvalidDataException("Invalid statechart POSIX ownership channel.");
        PosixStatechartFiles.RequireLinux();
        PosixStatechartProcess.BecomeGroupLeader();
        using var channel = await PosixStatechartChannel.ConnectAsync(arguments[0]);
        await using var anchor = await PosixStatechartAnchor.StartAsync(StatechartExecutable.Current());
        using var outer = new CancellationTokenSource();
        if (Environment.GetEnvironmentVariable("DYDO_STATECHART_NPM_LIFETIME") == "1")
            _ = StatechartOuterLifetime.ObserveAsync(Console.OpenStandardInput(), outer);
        var owner = new PosixStatechartOwner(channel, anchor, arguments, Task.Delay(Timeout.Infinite, outer.Token));
        try { return await owner.ControlAsync(); }
        finally { await owner.SettleAsync(); }
    }

    private async Task<int> ControlAsync()
    {
        await _channel.SendAsync("ready:1:" + _nonce + ":" + _anchor.Group);
        if (await NextAsync() != "reserve:" + _nonce) return 2;
        _stage = StatechartScratch.Reserve(_arguments[2], _arguments[3]);
        _temp = StatechartScratch.Reserve(_arguments[4], _arguments[3]);
        await _channel.SendAsync("created:" + StatechartCleanup.Encode(_stage.Path) + ":" + StatechartCleanup.Encode(_temp.Path));
        if (await NextAsync() != "register:" + _nonce) return 2;
        _stage.Register();
        _temp.Register();
        await _channel.SendAsync("registered:" + _nonce);
        while (true)
        {
            var frame = await NextAsync();
            if (frame?.StartsWith("handoff:" + _nonce + ":", StringComparison.Ordinal) == true && !_handedOff && !_drained)
                await HandoffAsync(frame);
            else if (frame?.StartsWith("attach:" + _nonce + ":", StringComparison.Ordinal) == true && _handedOff && !_drained)
                await AttachAsync(frame);
            else if (frame == "drain:" + _nonce && !_drained) await DrainAsync();
            else if (_drained && frame == "complete:" + _nonce) return await CompleteAsync();
            else if (_drained && frame?.StartsWith("result:" + _nonce + ":", StringComparison.Ordinal) == true)
                await ResultAsync(frame);
            else return 2;
        }
    }

    private async Task HandoffAsync(string frame)
    {
        var fields = frame.Split(':');
        if (fields is not ["handoff", _, var path]) throw new InvalidDataException("Invalid POSIX handoff frame.");
        if (path != "-")
        {
            _requestPath = StatechartCleanup.Decode(path);
            _requests = new StatechartRequests(StatechartHome.Current(), new StatechartAssets(), RuntimeInformation.RuntimeIdentifier);
            // Capture input/output validation before publication makes the destination nonempty.
            _request = _requests.Load(_requestPath);
            _requestDigest = RequestDigest(_requestPath);
        }
        _handedOff = true;
        await _channel.SendAsync("handoff:" + _nonce);
    }

    private async Task AttachAsync(string frame)
    {
        var fields = frame.Split(':');
        if (fields is not ["attach", _, var process] || !int.TryParse(process, out var pid) || pid <= 0)
            throw new InvalidDataException("Invalid POSIX checking-child frame.");
        PosixStatechartProcess.RequireGroup(pid, _anchor.Group);
        await _channel.SendAsync("attached:" + _nonce + ":" + pid);
    }

    private async Task<string?> NextAsync()
    {
        var input = _channel.ReadAsync();
        var selected = _drained ? await Task.WhenAny(input, _outerLost)
            : await Task.WhenAny(input, _anchor.Exited, _outerLost);
        if (selected != input) throw new IOException("Statechart group anchor or npm lifetime lost; no checking release.");
        return await input;
    }

    private async Task DrainAsync()
    {
        if (!await _anchor.DrainAsync()) throw new IOException("POSIX statechart group drain uncertain; scratch retained.");
        _drained = true;
        await _channel.SendAsync("drained:" + _nonce);
    }

    private async Task<int> CompleteAsync()
    {
        var stage = _stage!.CleanupAfterDrain();
        var temp = _temp!.CleanupAfterDrain();
        if (!stage || !temp) return 2;
        _completed = true;
        await _channel.SendAsync("cleaned:" + _nonce);
        return 0;
    }

    private async Task ResultAsync(string frame)
    {
        var fields = frame.Split(':');
        if (fields is not ["result", _, var code] || !int.TryParse(code, out var verdict) || verdict is not (0 or 1) ||
            !_handedOff || _resultRecorded || _requests is null || _request is null || _requestPath is null)
            throw new InvalidDataException("Invalid statechart completed-request frame.");
        if (RequestDigest(_requestPath) != _requestDigest)
            throw new IOException("Saved statechart request changed during POSIX checking.");
        _requests.Load(_requestPath, validateInputs: false);
        _requests.Complete(_requestPath, _request, verdict);
        _resultRecorded = true;
        await _channel.SendAsync("result:" + _nonce);
    }

    private async Task SettleAsync()
    {
        try
        {
            if (!_completed)
            {
                if (!_drained) _drained = await _anchor.DrainAsync();
                if (_drained)
                {
                    _stage?.CleanupAfterDrain();
                    _temp?.CleanupAfterDrain();
                }
                else RetainUncertain();
            }
        }
        catch
        {
            RetainUncertain();
            throw;
        }
        finally
        {
            _stage?.Dispose();
            _temp?.Dispose();
        }
    }

    private void RetainUncertain()
    {
        _stage?.Retain("POSIX owner/control loss without authoritative group drain; scratch retained");
        _temp?.Retain("POSIX owner/control loss without authoritative group drain; scratch retained");
    }

    private static string RequestDigest(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
}

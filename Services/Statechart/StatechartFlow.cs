namespace DynaDocs.Services.Statechart;

using System.Security.Cryptography;
using System.Text.Json.Nodes;

internal sealed class StatechartFlow(StatechartHome home, StatechartAssets assets, string rid)
{
    public async Task<int> StartAsync(string[] arguments, string caller, TextWriter output, CancellationToken token)
    {
        RequirePlatform();
        token.ThrowIfCancellationRequested();
        var request = StatechartRequest.Capture(arguments, caller, assets, rid);
        var payload = assets.Extract(home.Assets);
        var managed = new StatechartManaged(home, assets, rid);
        var dependencies = await StatechartResolver.ResolveAsync(request, managed, home, token);
        if (StatechartManaged.Missing(dependencies).Length != 0)
        {
            var store = new StatechartRequests(home, assets, rid);
            var path = store.Save(request);
            await output.WriteLineAsync(Plan(request, path, dependencies, managed.Root).ToJsonString());
            return 3;
        }
        await StatechartReady.VerifyAsync(dependencies, payload, home, full: false, token);
        return await StatechartRunner.RunAsync(request, dependencies, payload, home, output, token);
    }

    public async Task<int> ResumeAsync(string path, TextWriter output, CancellationToken token)
    {
        RequirePlatform();
        token.ThrowIfCancellationRequested();
        var store = new StatechartRequests(home, assets, rid);
        using var executionLock = store.Lock(path);
        try
        {
            var request = store.Load(path, validateInputs: false);
            if (store.Completed(path, request) is { } completed) return completed;
            StatechartRequest.Validate(request, assets, rid);
            var payload = assets.Extract(home.Assets);
            var managed = new StatechartManaged(home, assets, rid);
            var dependencies = await StatechartResolver.ResolveAsync(request, managed, home, token);
            if (StatechartManaged.Missing(dependencies).Length != 0)
                dependencies = await managed.InstallAsync(dependencies, payload, token);
            else await StatechartReady.VerifyAsync(dependencies, payload, home, full: false, token);
            StatechartRequest.Validate(request, assets, rid);
            var status = await StatechartRunner.RunAsync(request, dependencies, payload, home, output, token,
                new StatechartContinuation(path, executionLock));
            if (status is 0 or 1) store.Complete(path, request, status);
            return status;
        }
        catch (Exception error)
        {
            store.Failure(path, error.Message);
            throw;
        }
    }

    private JsonObject Plan(StatechartRequest request, string path, StatechartDependencies dependencies, string destination)
    {
        var selected = StatechartDownloads.Select(assets, rid);
        var missing = StatechartManaged.Missing(dependencies);
        var downloads = new JsonArray(selected.Where(item => missing.Contains(item.Name, StringComparer.Ordinal))
            .Select(item => (JsonNode)new JsonObject { ["name"] = item.Name, ["url"] = item.Url,
                ["integrity"] = item.Integrity, ["compressedByteLimit"] = item.Limit,
                ["destination"] = Path.Combine(destination, item.Name) }).ToArray());
        var self = StatechartExecutable.Current();
        var continuation = new[] { self.FileName }.Concat(self.ArgumentList).Concat(["statechart", "resume", "--request", path]);
        return new JsonObject { ["schema"] = 1, ["state"] = "approval-required", ["requestId"] = request.Id,
            ["request"] = path, ["requestDigest"] = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))),
            ["payloadDigest"] = assets.Digest, ["lockDigest"] = assets.LockDigest,
            ["missing"] = downloads, ["expandedByteLimitPerArchive"] = 1024L * 1024 * 1024,
            ["entryLimitPerArchive"] = 20000, ["connectSeconds"] = 15, ["downloadSecondsPerArchive"] = 120,
            ["setupSeconds"] = 600, ["caller"] = request.Caller, ["spec"] = request.Spec.Path,
            ["config"] = request.Config.Path, ["view"] = request.View?.Path, ["output"] = request.Output,
            ["installAndContinue"] = new JsonArray(continuation.Select(argument => (JsonNode)JsonValue.Create(argument)!).ToArray()),
            ["message"] = "Statechart prerequisites are missing. Submit this exact continuation through the host's normal installation authorization; no installation has started." };
    }

    private static void RequirePlatform()
    {
        if (!OperatingSystem.IsWindows()) PosixStatechartFiles.RequireLinux();
    }
}

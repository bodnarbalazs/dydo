namespace DynaDocs.Services.Statechart;

using System.CommandLine;
using System.Security.Cryptography;
using System.Text.Json;

internal sealed record StatechartRequest
{
    public int Schema { get; init; } = 1;
    public int Protocol { get; init; } = 1;
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public required string[] Arguments { get; init; }
    public required string Caller { get; init; }
    public required StatechartInput Spec { get; init; }
    public required StatechartInput Config { get; init; }
    public StatechartInput? View { get; init; }
    public required string Output { get; init; }
    public string? Java { get; init; }
    public string? Tlc { get; init; }
    public string? Skill { get; init; }
    public required string Rid { get; init; }
    public required string Release { get; init; }
    public required string PayloadDigest { get; init; }
    public required string LockDigest { get; init; }

    public static StatechartRequest Capture(IReadOnlyList<string> arguments, string caller,
        StatechartAssets assets, string rid) => Capture(arguments, caller, assets, rid,
            Environment.GetEnvironmentVariable("JAVA"), Environment.GetEnvironmentVariable("TLA2TOOLS"));

    public static void Validate(StatechartRequest request, StatechartAssets assets, string rid)
    {
        ValidateIdentity(request, assets, rid);
        var current = Capture(request.Arguments, request.Caller, assets, rid, request.Java, request.Tlc)
            with { Id = request.Id };
        if (JsonSerializer.Serialize(request, StatechartRequestJson.Default.StatechartRequest) !=
            JsonSerializer.Serialize(current, StatechartRequestJson.Default.StatechartRequest))
            throw new IOException("Saved statechart inputs, paths or implementation identities changed; prepare a fresh request.");
    }

    public static void ValidateIdentity(StatechartRequest request, StatechartAssets assets, string rid)
    {
        if (request.Schema != 1 || request.Protocol != 1 || !Guid.TryParseExact(request.Id, "N", out _) ||
            request.Rid != rid || request.Release != assets.Release || request.PayloadDigest != assets.Digest ||
            request.LockDigest != assets.LockDigest || request.Arguments is null || request.Caller is null ||
            request.Spec is null || request.Config is null || request.Output is null)
            throw new IOException("Unsupported, stale or malformed saved statechart request; prepare a fresh request.");
    }

    private static StatechartRequest Capture(IReadOnlyList<string> arguments, string caller,
        StatechartAssets assets, string rid, string? java, string? tlc)
    {
        if (!Path.IsPathFullyQualified(caller) || !Directory.Exists(caller))
            throw new IOException("Statechart caller directory must exist and be absolute.");
        using var dependencyLock = JsonDocument.Parse(assets.DependencyLock());
        if (!dependencyLock.RootElement.GetProperty("platforms").EnumerateArray()
                .Any(platform => platform.GetProperty("rid").GetString() == rid))
            throw new IOException("Unsupported statechart runtime: " + rid);
        var spec = new Argument<string>("spec");
        var config = new Option<string?>("--config");
        var view = new Option<string?>("--view");
        var output = new Option<string?>("--out");
        var skill = new Option<string?>("--skill");
        var command = new Command("statechart");
        command.Arguments.Add(spec);
        foreach (var option in new[] { config, view, output, skill }) command.Options.Add(option);
        var parsed = command.Parse(arguments.ToArray());
        if (parsed.Errors.Count != 0) throw new IOException(string.Join(" ", parsed.Errors.Select(error => error.Message)));
        var model = parsed.GetValue(spec)!;
        var defaultBase = model.EndsWith(".tla", StringComparison.Ordinal) ? model[..^4] : model;
        return new StatechartRequest
        {
            Arguments = arguments.ToArray(), Caller = Path.GetFullPath(caller), Rid = rid,
            Spec = Input(model, caller), Config = Input(parsed.GetValue(config) ?? defaultBase + ".cfg", caller),
            View = parsed.GetValue(view) is { } file ? Input(file, caller) : null,
            Output = Destination(parsed.GetValue(output) ?? defaultBase + "-chart", caller),
            Java = java, Tlc = tlc, Release = assets.Release, PayloadDigest = assets.Digest, LockDigest = assets.LockDigest,
            Skill = parsed.GetValue(skill) is { } installed ? StatechartSkill.Validate(Path.GetFullPath(installed, caller), assets) : null
        };
    }

    private static StatechartInput Input(string file, string caller)
    {
        var path = Path.GetFullPath(file, caller);
        using var stream = File.OpenRead(path);
        return new StatechartInput(path, Convert.ToHexStringLower(SHA256.HashData(stream)));
    }

    private static string Destination(string path, string caller)
    {
        var target = Path.GetFullPath(path, caller);
        var info = new DirectoryInfo(target);
        if ((info.LinkTarget is not null) || File.Exists(target))
            throw new IOException("Statechart output must be a new or empty plain directory: " + target);
        if (info.Exists && info.EnumerateFileSystemInfos().Any())
            throw new IOException("Statechart output is not empty: " + target);
        var ancestor = info.Exists ? info : info.Parent;
        while (ancestor is not null && !ancestor.Exists) ancestor = ancestor.Parent;
        if (ancestor is null) throw new IOException("Statechart output has no existing parent.");
        StatechartPaths.RequirePlainDirectory(ancestor.FullName);
        return target;
    }
}

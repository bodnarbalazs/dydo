namespace DynaDocs.Services.Statechart;

using System.Text.Json.Nodes;

internal static class StatechartSkill
{
    private const string Receipt = ".statechart-skill.json";

    public static string Validate(string directory, StatechartAssets assets)
    {
        var info = new DirectoryInfo(directory);
        directory = info.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? info.FullName;
        StatechartPaths.RequirePlainDirectory(directory);
        var receipt = Path.Combine(directory, Receipt);
        if (!File.Exists(receipt) || (File.GetAttributes(receipt) & FileAttributes.ReparsePoint) != 0 ||
            new FileInfo(receipt).Length > 4096)
            throw new IOException("Missing or invalid installed statechart skill receipt; install the matching skill with dydo statechart install-skill --root PROJECT.");
        var identity = JsonNode.Parse(File.ReadAllBytes(receipt));
        if (identity?["schema"]?.GetValue<int>() != 1 || identity["protocol"]?.GetValue<int>() != 1 ||
            identity["release"]?.GetValue<string>() != assets.Release || identity["payload"]?.GetValue<string>() != assets.Digest ||
            identity["lock"]?.GetValue<string>() != assets.LockDigest)
            throw new IOException("Installed statechart skill does not match this dydo release/protocol/payload; install into a fresh project or explicitly reconcile local files before dydo statechart install-skill --root PROJECT.");
        assets.VerifySkill(directory);
        return directory;
    }

    public static async Task InstallAsync(string project, StatechartHome home, StatechartAssets assets,
        string rid, TextWriter output, CancellationToken token)
    {
        if (!Path.IsPathFullyQualified(project)) throw new IOException("Skill installation requires an explicit absolute project root.");
        StatechartPaths.RequirePlainDirectory(project);
        var payload = assets.Extract(home.Assets);
        var target = Path.Combine(project, "skills", "engineering", "statechart");
        if (Directory.Exists(target)) Validate(target, assets);
        else
        {
            StatechartPaths.EnsureDirectory(Path.GetDirectoryName(target)!);
            if (File.Exists(target) || new DirectoryInfo(target).LinkTarget is not null)
                throw new IOException("Foreign statechart skill path retained: " + target);
            var stage = Path.Combine(Path.GetDirectoryName(target)!, ".statechart-skill-" + Guid.NewGuid().ToString("N"));
            Copy(Path.Combine(payload, "skills", "engineering", "statechart"), stage);
            var receipt = new JsonObject { ["schema"] = 1, ["protocol"] = 1, ["release"] = assets.Release,
                ["payload"] = assets.Digest, ["lock"] = assets.LockDigest };
            await File.WriteAllTextAsync(Path.Combine(stage, Receipt), receipt.ToJsonString(), token);
            Validate(stage, assets);
            Directory.Move(stage, target);
        }
        var node = new StatechartManaged(home, assets, rid).Read().Node ??
            StatechartPrerequisites.Find("node", Environment.GetEnvironmentVariable("PATH")) ??
            throw new IOException("No usable Node for the embedded project projection installer; statechart invocation can prepare its locked prerequisite plan.");
        StatechartPaths.EnsureDirectory(home.State);
        await using var owner = await StatechartLifetime.CreateAsync(StatechartExecutable.Current(),
            home.State, home.State, Path.TrimEndingDirectorySeparator(Path.GetTempPath()));
        await owner.RegisterAsync();
        try
        {
            var result = await StatechartProbe.CaptureAsync(node, [Path.Combine(payload, "setup-skills.mjs"),
                "--root", project, "--skill", "statechart"], owner, project, token);
            await output.WriteLineAsync(result);
        }
        finally
        {
            if (!await owner.CompleteAsync()) throw new IOException("Skill projection cleanup drain uncertain; scratch retained.");
        }
    }

    private static void Copy(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var entry in Directory.EnumerateFileSystemEntries(source))
        {
            var destination = Path.Combine(target, Path.GetFileName(entry));
            if (Directory.Exists(entry)) Copy(entry, destination);
            else File.Copy(entry, destination, overwrite: false);
        }
    }
}

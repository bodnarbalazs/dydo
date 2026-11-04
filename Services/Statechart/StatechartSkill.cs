namespace DynaDocs.Services.Statechart;

using System.Text.Json;
using System.Text.Json.Nodes;

internal static class StatechartSkill
{
    private const string Receipt = ".statechart-skill.json";

    // The installed skill a chart invocation names: this release's, holding the files dydo installed.
    public static string Validate(string directory, StatechartAssets assets)
    {
        var info = new DirectoryInfo(directory);
        directory = info.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? info.FullName;
        var identity = Identity(directory);
        if (Current(identity, assets)) Verify(directory, assets);
        else if (Change(directory, identity, assets) is { } change) throw new IOException(Edited(directory, change));
        else throw new IOException($"The statechart skill in {directory} is unchanged since an earlier dydo release installed it; " +
            $"update it with: dydo statechart install-skill --root {Project(directory)}");
        return directory;
    }

    public static async Task InstallAsync(string project, StatechartHome home, StatechartAssets assets,
        string rid, TextWriter output, CancellationToken token)
    {
        if (!Path.IsPathFullyQualified(project)) throw new IOException("Skill installation requires an explicit absolute project root.");
        StatechartPaths.RequirePlainDirectory(project);
        var payload = await assets.ExtractAsync(home.Assets, token);
        Place(project, payload, assets);
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

    // The project's canonical skill: installed when absent, replaced when an earlier release left it unchanged.
    internal static string Place(string project, string payload, StatechartAssets assets)
    {
        var target = Path.Combine(project, "skills", "engineering", "statechart");
        var parent = Path.GetDirectoryName(target)!;
        if (Directory.Exists(target))
        {
            var identity = Identity(target);
            if (Current(identity, assets))
            {
                Verify(target, assets);
                return target;
            }
            if (Change(target, identity, assets) is { } change) throw new IOException(Edited(target, change));
        }
        else
        {
            StatechartPaths.EnsureDirectory(parent);
            if (File.Exists(target) || new DirectoryInfo(target).LinkTarget is not null)
                throw new IOException("Foreign statechart skill path retained: " + target);
        }
        var stage = Path.Combine(parent, ".statechart-skill-" + Guid.NewGuid().ToString("N"));
        var replaced = stage + "-replaced";
        try
        {
            Copy(Path.Combine(payload, "skills", "engineering", "statechart"), stage);
            var receipt = new JsonObject { ["schema"] = 1, ["protocol"] = 1, ["release"] = assets.Release,
                ["payload"] = assets.Digest, ["lock"] = assets.LockDigest, ["files"] = StatechartInventory.Capture(stage) };
            File.WriteAllText(Path.Combine(stage, Receipt), receipt.ToJsonString());
            Verify(stage, assets);
            Swap(stage, target, replaced);
        }
        finally
        {
            foreach (var path in new[] { stage, replaced }.Where(Directory.Exists)) Directory.Delete(path, recursive: true);
        }
        return target;
    }

    private static void Swap(string stage, string target, string replaced)
    {
        if (!Directory.Exists(target))
        {
            Directory.Move(stage, target);
            return;
        }
        Directory.Move(target, replaced);
        try { Directory.Move(stage, target); }
        catch
        {
            Directory.Move(replaced, target);
            throw;
        }
    }

    private static JsonObject Identity(string directory)
    {
        StatechartPaths.RequirePlainDirectory(directory);
        var receipt = new FileInfo(Path.Combine(directory, Receipt));
        if (receipt.Exists && (receipt.Attributes & FileAttributes.ReparsePoint) == 0 && receipt.Length <= 1024 * 1024)
        {
            try
            {
                if (JsonNode.Parse(File.ReadAllBytes(receipt.FullName)) is JsonObject identity && identity["schema"]?.GetValue<int>() == 1)
                    return identity;
            }
            catch (Exception error) when (error is JsonException or InvalidOperationException) { }
        }
        throw new IOException($"{directory} holds no statechart skill receipt that dydo wrote, so dydo leaves it untouched. " +
            $"Move it aside, then run: dydo statechart install-skill --root {Project(directory)}");
    }

    private static bool Current(JsonObject identity, StatechartAssets assets) =>
        identity["protocol"]?.GetValue<int>() == 1 && identity["release"]?.GetValue<string>() == assets.Release &&
        identity["payload"]?.GetValue<string>() == assets.Digest && identity["lock"]?.GetValue<string>() == assets.LockDigest;

    // Null while the files are those dydo installed. Without a recorded inventory only this release's bytes qualify.
    private static string? Change(string directory, JsonObject identity, StatechartAssets assets)
    {
        try
        {
            if (identity["files"] is not JsonArray recorded)
            {
                assets.VerifySkill(directory);
                return null;
            }
            var before = Rows(recorded);
            var after = Rows(StatechartInventory.Capture(directory, Receipt));
            var changed = before.Keys.Union(after.Keys).Where(path => before.GetValueOrDefault(path) != after.GetValueOrDefault(path))
                .Order(StringComparer.Ordinal).ToArray();
            return changed.Length == 0 ? null : "changed since install: " + string.Join(", ", changed);
        }
        catch (Exception error) when (error is IOException or InvalidOperationException) { return error.Message; }
    }

    private static Dictionary<string, string> Rows(JsonArray inventory) =>
        inventory.ToDictionary(row => row!["path"]!.GetValue<string>(), row => row!.ToJsonString(), StringComparer.Ordinal);

    private static void Verify(string directory, StatechartAssets assets)
    {
        try { assets.VerifySkill(directory); }
        catch (IOException error) { throw new IOException(Edited(directory, error.Message), error); }
    }

    private static string Edited(string directory, string change) =>
        $"The statechart skill in {directory} differs from what dydo installed ({change}); dydo leaves it untouched. " +
        $"Move it aside, run dydo statechart install-skill --root {Project(directory)}, then re-apply your edits.";

    // install-skill places the skill at PROJECT/skills/engineering/statechart.
    private static string Project(string directory)
    {
        var engineering = Path.GetDirectoryName(directory) ?? "";
        var skills = Path.GetDirectoryName(engineering) ?? "";
        var placed = Path.GetFileName(directory) == "statechart" && Path.GetFileName(engineering) == "engineering" &&
            Path.GetFileName(skills) == "skills";
        return placed ? Path.GetDirectoryName(skills) ?? "PROJECT" : "PROJECT";
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

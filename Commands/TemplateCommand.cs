namespace DynaDocs.Commands;

using System.CommandLine;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Serialization;
using DynaDocs.Utils;

public static class TemplateCommand
{
    // Framework-owned files relative to the dydo root
    public static readonly string[] FrameworkDocFiles =
    [
        "reference/about-dynadocs.md",
        "reference/dydo-commands.md",
        "reference/dydo-glossary.md",
        "reference/linear-workspace-standard.md",
        "reference/writing-docs.md",
        "guides/working-tree-contract.md"
    ];

    // Binary assets retired from the framework, deleted from projects on update — but only
    // when the on-disk copy is a known framework version (stored hash, or a shipped hash
    // listed here). A user-modified copy is kept and becomes a user-owned asset once its
    // stale hash entry is pruned. Currently: the pre-DR-041 architecture diagram, which
    // depicted the removed claim/inbox/agent-workspace runtime (issue 0301).
    internal static readonly (string RelativePath, string[] KnownHashes)[] RetiredBinaryFiles =
    [
        ("_assets/dydo-diagram.svg",
            ["d93720f85fc71f4a75798a364d783c18e70c94fd349fa462919e10cbc9c223b9"]),
    ];

    // Framework docs retired from the framework, deleted from projects on update under the same
    // ownership rule as the binaries: an unmodified copy (stored hash, or a shipped hash listed
    // here) is removed; a user-edited copy is kept and becomes user-owned once its stale hash
    // entry is pruned. Currently: the navigation guide, retired with the generated-hub
    // experiment's verdict (DYD-68) — agents navigate by grep and direct links.
    internal static readonly (string RelativePath, string[] KnownHashes)[] RetiredDocFiles =
    [
        ("guides/how-to-use-docs.md", []),
    ];

    public static Command Create()
    {
        var command = new Command("template", "Manage templates");
        command.Subcommands.Add(CreateUpdateCommand());
        return command;
    }

    private static Command CreateUpdateCommand()
    {
        var diffOption = new Option<bool>("--diff") { Description = "Preview changes without writing" };

        var command = new Command("update", "Update framework docs");
        command.Options.Add(diffOption);

        command.SetAction(parseResult => ExecuteUpdate(parseResult.GetValue(diffOption)));

        return command;
    }

    private static int ExecuteUpdate(bool diff)
    {
        try
        {
            var configService = new ConfigService();
            var configPath = configService.FindConfigFile();
            if (configPath == null)
            {
                Console.Error.WriteLine("No dydo.json found. Run 'dydo init' first.");
                return 1;
            }

            var original = configService.LoadConfigStrict()!;
            var config = PrepareSourceUpdate(original, Path.GetDirectoryName(configPath)!);
            var dydoRoot = Path.Combine(Path.GetDirectoryName(configPath)!, config.Structure.Root);

            var tally = new UpdateTally();
            ApplySourceUpdate(original, config, dydoRoot, diff, tally);
            foreach (var relativePath in FrameworkDocFiles)
            {
                var before = config.FrameworkHashes.GetValueOrDefault(relativePath);
                var result = UpdateDocFile(relativePath, dydoRoot, config, diff);
                AccumulateResult(result, tally);
                if (result is UpdateResult.Skipped
                    && before != config.FrameworkHashes.GetValueOrDefault(relativePath))
                    tally.MetadataRefreshed++;
            }

            tally.Updated += CleanRetiredBinaries(dydoRoot, config, diff);
            tally.Updated += CleanRetiredDocs(dydoRoot, config, diff);
            PruneStaleHashes(config, diff);

            tally.Updated += ApplyConfigDefaults(config);
            tally.Updated += EnsureTypesJson(dydoRoot, diff);

            if (!diff)
                configService.SaveConfig(config, configPath);

            ReportSummary(tally);

            return tally.Warnings.Count > 0 ? 1 : 0;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            Console.Error.WriteLine($"Template update failed: {ex.Message}");
            return 1;
        }
    }

    private static DydoConfig PrepareSourceUpdate(DydoConfig original, string projectRoot)
    {
        var json = JsonSerializer.Serialize(original, DydoConfigJsonContext.Default.DydoConfig);
        var planned = JsonSerializer.Deserialize(json, DydoConfigJsonContext.Default.DydoConfig)!;
        var packaged = TemplateGenerator.GetAllTemplateNames().ToHashSet(StringComparer.Ordinal);
        var sourceRoot = Path.Combine(projectRoot, original.Structure.Root, "_system", "templates");
        var managed = ManagedShippedSourcePaths(original).ToHashSet(StringComparer.Ordinal);

        foreach (var relative in managed.Where(relative =>
                     !packaged.Contains(Path.GetFileName(relative))
                     && !original.FrameworkHashes.ContainsKey(relative)))
        {
            var path = Path.Combine(projectRoot, original.Structure.Root,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
                throw new InvalidDataException(
                    $"Local custom source '{Path.GetFileName(relative)}' collides with a retired shipped template.");
        }

        foreach (var name in packaged)
        {
            var path = Path.Combine(sourceRoot, name);
            if (!File.Exists(path))
                continue;
            var relative = $"_system/templates/{name}";
            if (!managed.Contains(relative))
                throw new InvalidDataException($"Local custom source '{name}' collides with a shipped template.");
        }

        var temporaryRoot = Path.Combine(Path.GetTempPath(), "dydo-update-" + Guid.NewGuid().ToString("N"));
        try
        {
            var currentDydo = Path.Combine(projectRoot, original.Structure.Root);
            var temporaryDydo = Path.Combine(temporaryRoot, original.Structure.Root);
            if (Directory.Exists(currentDydo))
                CopyDirectory(currentDydo, temporaryDydo, currentDydo);
            Directory.CreateDirectory(Path.Combine(temporaryDydo, "_system", "templates"));

            foreach (var relative in managed.Where(relative =>
                         !packaged.Contains(Path.GetFileName(relative))))
            {
                var path = Path.Combine(temporaryDydo, relative.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(path))
                    File.Delete(path);
            }

            var temporarySources = Path.Combine(temporaryDydo, "_system", "templates");
            foreach (var name in packaged)
                File.WriteAllText(Path.Combine(temporarySources, name), TemplateGenerator.ReadBuiltInTemplate(name));
            foreach (var relativePath in FrameworkDocFiles)
            {
                var path = Path.Combine(temporaryDydo, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path) && GetEmbeddedDocContent(relativePath) is { } content)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, content);
                }
            }

            foreach (var key in planned.FrameworkHashes.Keys
                         .Where(key => key.StartsWith("_system/templates/", StringComparison.Ordinal)
                             && !packaged.Contains(Path.GetFileName(key)))
                         .ToList())
                planned.FrameworkHashes.Remove(key);
            foreach (var name in packaged)
                planned.FrameworkHashes[$"_system/templates/{name}"] = ComputeHash(TemplateGenerator.ReadBuiltInTemplate(name));

            SkillTemplateService.DiscoverLocalCatalog(temporaryRoot, planned);
            return planned;
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
                Directory.Delete(temporaryRoot, true);
        }
    }

    private static void ApplySourceUpdate(
        DydoConfig original,
        DydoConfig planned,
        string dydoRoot,
        bool diff,
        UpdateTally tally)
    {
        var sourceRoot = Path.Combine(dydoRoot, "_system", "templates");
        var packaged = TemplateGenerator.GetAllTemplateNames().ToHashSet(StringComparer.Ordinal);
        foreach (var relative in ManagedShippedSourcePaths(original)
                     .Where(relative => !packaged.Contains(Path.GetFileName(relative))))
        {
            var path = Path.Combine(dydoRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
                continue;
            if (!diff)
                File.Delete(path);
            Console.WriteLine($"  Removed retired source: {relative}");
            tally.Updated++;
        }

        foreach (var name in packaged.OrderBy(name => name, StringComparer.Ordinal))
        {
            var path = Path.Combine(sourceRoot, name);
            var content = TemplateGenerator.ReadBuiltInTemplate(name);
            if (File.Exists(path) && File.ReadAllText(path) == content)
                continue;
            var action = File.Exists(path) ? "Updated" : "Created";
            if (!diff)
            {
                Directory.CreateDirectory(sourceRoot);
                File.WriteAllText(path, content);
            }
            Console.WriteLine($"  {action} source: _system/templates/{name}");
            tally.Updated++;
        }

        foreach (var relative in original.FrameworkHashes.Keys.Union(planned.FrameworkHashes.Keys)
                     .Where(key => key.StartsWith("_system/templates/", StringComparison.Ordinal))
                     .OrderBy(key => key, StringComparer.Ordinal))
        {
            if (original.FrameworkHashes.GetValueOrDefault(relative) == planned.FrameworkHashes.GetValueOrDefault(relative))
                continue;
            var action = planned.FrameworkHashes.ContainsKey(relative) ? "Reconciled" : "Removed";
            Console.WriteLine($"  {action} source hash: {relative}");
            tally.SourceHashesRefreshed++;
        }

        ReportSwitchboardChanges(original.Skills, planned.Skills, tally);
        original.Skills = planned.Skills;
        original.FrameworkHashes = planned.FrameworkHashes;
    }

    private static void ReportSwitchboardChanges(
        IReadOnlyDictionary<string, SkillSwitchConfig> current,
        IReadOnlyDictionary<string, SkillSwitchConfig> planned,
        UpdateTally tally)
    {
        foreach (var (name, next) in planned)
        {
            if (!current.TryGetValue(name, out var prior))
            {
                Console.WriteLine($"  Added skill switch: {name}");
                tally.SwitchboardChanges++;
            }
            else if (prior.Origin != next.Origin
                     || prior.EmitAgent != next.EmitAgent
                     || prior.CodexMetadata != next.CodexMetadata
                     || !(prior.Resources ?? []).SequenceEqual(next.Resources ?? [], StringComparer.Ordinal))
            {
                Console.WriteLine($"  Reconciled skill switch provenance: {name}");
                tally.SwitchboardChanges++;
            }
        }
    }

    private static IEnumerable<string> ManagedShippedSourcePaths(DydoConfig config)
    {
        var paths = config.FrameworkHashes.Keys
            .Where(key => key.StartsWith("_system/templates/", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var file = path["_system/templates/".Length..];
            if (!IsSourceTemplateName(file))
                throw new InvalidDataException($"Invalid local source hash path: '{path}'.");
        }
        foreach (var (name, skill) in config.Skills.Where(entry => entry.Value.Origin == "shipped"))
        {
            paths.Add($"_system/templates/skill-{name}.template.md");
            foreach (var resource in skill.Resources ?? [])
                paths.Add($"_system/templates/{name}-resource-{resource}.template.md");
        }
        return paths;
    }

    private static bool IsSourceTemplateName(string file)
    {
        if (!file.EndsWith(".template.md", StringComparison.Ordinal))
            return false;
        var stem = file[..^".template.md".Length];
        if (stem.StartsWith("skill-", StringComparison.Ordinal) && ConfigService.IsValidSlug(stem["skill-".Length..]))
            return true;
        var parts = stem.Split("-resource-", StringSplitOptions.None);
        return parts.Length == 2 && parts.All(ConfigService.IsValidSlug);
    }

    private static void CopyDirectory(string source, string destination, string dydoRoot)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        foreach (var directory in Directory.GetDirectories(source))
        {
            var relative = Path.GetRelativePath(dydoRoot, directory)
                .Replace(Path.DirectorySeparatorChar, '/');
            if (relative.Equals("agents/workspace", StringComparison.OrdinalIgnoreCase))
                continue;
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)), dydoRoot);
        }
    }

    private static int ApplyConfigDefaults(DydoConfig config)
    {
        var updated = 0;

        var nudgesAdded = ConfigFactory.EnsureDefaultNudges(config);
        if (nudgesAdded > 0)
        {
            Console.WriteLine($"  Added {nudgesAdded} default nudge(s)");
            updated += nudgesAdded;
        }

        updated += EnsureScanExcludeWithReport(config);
        return updated;
    }

    private static void ReportSummary(UpdateTally tally)
    {
        var summary = $"Template update complete: {tally.Updated} updated, {tally.Skipped} already current";
        if (tally.MetadataRefreshed > 0)
            summary += $", {tally.MetadataRefreshed} metadata-only document hash refresh(es)";
        if (tally.SourceHashesRefreshed > 0)
            summary += $", {tally.SourceHashesRefreshed} source hash change(s)";
        if (tally.SwitchboardChanges > 0)
            summary += $", {tally.SwitchboardChanges} switchboard change(s)";
        if (tally.Warned > 0)
            summary += $", {tally.Warned} warned";
        Console.WriteLine(summary + ".");

        foreach (var warning in tally.Warnings)
            Console.Error.WriteLine($"  Warning: {warning}");
    }

    private static void AccumulateResult(UpdateResult result, UpdateTally tally)
    {
        switch (result)
        {
            case UpdateResult.Updated:
                tally.Updated++;
                break;
            case UpdateResult.Skipped:
                tally.Skipped++;
                break;
            case UpdateResult.Warning warning:
                tally.Warnings.Add(warning.Message);
                tally.Warned++;
                break;
        }
    }

    /// <summary>Deletes retired framework binaries from the project when the on-disk copy is a
    /// known framework version; runs before <see cref="PruneStaleHashes"/> so the stored hash is
    /// still available for the ownership check.</summary>
    private static int CleanRetiredBinaries(string dydoRoot, DydoConfig config, bool diff)
    {
        var removed = 0;
        foreach (var (relativePath, knownHashes) in RetiredBinaryFiles)
        {
            var fullPath = Path.Combine(dydoRoot, relativePath);
            if (!File.Exists(fullPath))
                continue;

            var onDiskHash = ComputeHashBytes(File.ReadAllBytes(fullPath));
            var storedHash = config.FrameworkHashes.GetValueOrDefault(relativePath);
            if (onDiskHash != storedHash && !knownHashes.Contains(onDiskHash))
            {
                Console.WriteLine($"  Kept: {relativePath} — retired from the framework, but modified; now a user-owned asset");
                continue;
            }

            if (!diff)
                File.Delete(fullPath);
            Console.WriteLine($"  Removed retired: {relativePath}");
            removed++;
        }
        return removed;
    }

    /// <summary>Deletes retired framework docs from the project when the on-disk copy is a
    /// known framework version (text hash, matching how <see cref="UpdateDocFile"/> stores it);
    /// runs before <see cref="PruneStaleHashes"/> so the stored hash is still available for the
    /// ownership check.</summary>
    private static int CleanRetiredDocs(string dydoRoot, DydoConfig config, bool diff)
    {
        var removed = 0;
        foreach (var (relativePath, knownHashes) in RetiredDocFiles)
        {
            var fullPath = Path.Combine(dydoRoot, relativePath);
            if (!File.Exists(fullPath))
                continue;

            var onDiskHash = ComputeHash(File.ReadAllText(fullPath));
            var storedHash = config.FrameworkHashes.GetValueOrDefault(relativePath);
            if (onDiskHash != storedHash && !knownHashes.Contains(onDiskHash))
            {
                Console.WriteLine($"  Kept: {relativePath} — retired from the framework, but modified; now a user-owned document");
                continue;
            }

            if (!diff)
                File.Delete(fullPath);
            Console.WriteLine($"  Removed retired: {relativePath}");
            removed++;
        }
        return removed;
    }

    private static void PruneStaleHashes(DydoConfig config, bool diff)
    {
        var validKeys = new HashSet<string>(FrameworkDocFiles);
        validKeys.UnionWith(TemplateGenerator.GetAllTemplateNames()
            .Select(name => $"_system/templates/{name}"));
        var staleKeys = config.FrameworkHashes.Keys
            .Where(k => !validKeys.Contains(k))
            .ToList();
        foreach (var key in staleKeys)
        {
            if (!diff)
                config.FrameworkHashes.Remove(key);
            Console.WriteLine($"  Pruned stale hash: {key}");
        }
    }

    private static int EnsureScanExcludeWithReport(DydoConfig config)
    {
        var added = ConfigFactory.EnsureDefaultScanExclude(config);
        if (added > 0)
            Console.WriteLine($"  Added {added} default scan-exclude entry(ies)");
        return added;
    }

    private static int EnsureTypesJson(string dydoRoot, bool diff)
    {
        var path = Path.Combine(dydoRoot, FrontmatterTypesService.TypesJsonRelativePath);
        var baseline = TemplateGenerator.ReadBuiltInTemplate("types.json.template");

        if (!File.Exists(path))
        {
            if (!diff)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, baseline);
            }
            Console.WriteLine($"  Created: {FrontmatterTypesService.TypesJsonRelativePath}");
            return 1;
        }

        var existingTypes = ParseTypesJson(path);
        if (existingTypes == null)
        {
            Console.Error.WriteLine($"  Warning: {FrontmatterTypesService.TypesJsonRelativePath} is malformed; not auto-overwritten. Fix or delete the file and re-run.");
            return 0;
        }

        var baselineTypes = ParseTypesJsonFromString(baseline) ?? Array.Empty<string>();
        var existingSet = new HashSet<string>(existingTypes, StringComparer.Ordinal);
        var missing = baselineTypes.Where(t => !existingSet.Contains(t)).ToList();
        if (missing.Count == 0) return 0;

        if (!diff)
        {
            var merged = existingTypes.Concat(missing).ToList();
            WriteTypesJson(path, merged);
        }
        Console.WriteLine($"  Added {missing.Count} default type(s) to {FrontmatterTypesService.TypesJsonRelativePath}");
        return 1;
    }

    private static string[]? ParseTypesJson(string path)
    {
        try { return ParseTypesJsonFromString(File.ReadAllText(path)); }
        catch { return null; }
    }

    private static string[]? ParseTypesJsonFromString(string json)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize(
                json, Serialization.TypesJsonContext.Default.StringArray);
        }
        catch
        {
            return null;
        }
    }

    private static void WriteTypesJson(string path, List<string> types)
    {
        var sb = new StringBuilder();
        sb.Append("[\n");
        for (var i = 0; i < types.Count; i++)
        {
            sb.Append("  \"");
            sb.Append(EscapeJsonString(types[i]));
            sb.Append('"');
            if (i < types.Count - 1) sb.Append(',');
            sb.Append('\n');
        }
        sb.Append("]\n");
        File.WriteAllText(path, sb.ToString());
    }

    private static string EscapeJsonString(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20)
                        sb.Append($"\\u{(int)c:X4}");
                    else
                        sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    private static UpdateResult CreateFile(
        string fullPath, string relativePath, string content, DydoConfig config, bool diff)
    {
        if (!diff)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
            config.FrameworkHashes[relativePath] = ComputeHash(content);
        }
        Console.WriteLine($"  Created: {relativePath}");
        return new UpdateResult.Updated();
    }

    private static UpdateResult WriteUpdate(
        string fullPath, string relativePath, string content, DydoConfig config, bool diff)
    {
        if (!diff)
        {
            File.WriteAllText(fullPath, content);
            config.FrameworkHashes[relativePath] = ComputeHash(content);
        }
        Console.WriteLine($"  Updated: {relativePath}");
        return new UpdateResult.Updated();
    }

    private static UpdateResult UpdateDocFile(
        string relativePath, string dydoRoot, DydoConfig config, bool diff)
    {
        var fullPath = Path.Combine(dydoRoot, relativePath);
        var embeddedContent = GetEmbeddedDocContent(relativePath);
        if (embeddedContent == null)
            return new UpdateResult.Skipped();

        if (!File.Exists(fullPath))
            return CreateFile(fullPath, relativePath, embeddedContent, config, diff);

        var onDisk = File.ReadAllText(fullPath);
        if (NormalizeForHash(onDisk) == NormalizeForHash(embeddedContent))
        {
            config.FrameworkHashes[relativePath] = ComputeHash(embeddedContent);
            return new UpdateResult.Skipped();
        }

        var storedHash = config.FrameworkHashes.GetValueOrDefault(relativePath);
        var onDiskHash = ComputeHash(onDisk);

        if (storedHash != null && storedHash != onDiskHash)
        {
            Console.Error.WriteLine($"  Skipped: {relativePath} — user-edited (hash mismatch)");
            return new UpdateResult.Warning($"{relativePath}: user-edited, skipped");
        }

        return WriteUpdate(fullPath, relativePath, embeddedContent, config, diff);
    }

    private static string? GetEmbeddedDocContent(string relativePath) => relativePath switch
    {
        "reference/about-dynadocs.md" => TemplateGenerator.GenerateAboutDynadocsMd(),
        "reference/dydo-commands.md" => TemplateGenerator.GenerateDydoCommandsMd(),
        "reference/dydo-glossary.md" => TemplateGenerator.GenerateDydoGlossaryMd(),
        "reference/linear-workspace-standard.md" => TemplateGenerator.GenerateLinearWorkspaceStandardMd(),
        "reference/writing-docs.md" => TemplateGenerator.GenerateWritingDocsMd(),
        "guides/working-tree-contract.md" => TemplateGenerator.GenerateWorkingTreeContractMd(),
        _ => null
    };

    public static string NormalizeForHash(string content)
    {
        // Strip UTF-8 BOM
        if (content.Length > 0 && content[0] == '\uFEFF')
            content = content[1..];

        return content.Replace("\r\n", "\n");
    }

    public static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeForHash(content)));
        return Convert.ToHexStringLower(bytes);
    }

    public static string ComputeHashBytes(byte[] content)
    {
        var bytes = SHA256.HashData(content);
        return Convert.ToHexStringLower(bytes);
    }

    internal abstract record UpdateResult
    {
        public sealed record Updated : UpdateResult;
        public sealed record Skipped : UpdateResult;
        public sealed record Warning(string Message) : UpdateResult;
    }

    private sealed class UpdateTally
    {
        public int Updated;
        public int Skipped;
        public int Warned;
        public int MetadataRefreshed;
        public int SourceHashesRefreshed;
        public int SwitchboardChanges;
        public List<string> Warnings { get; } = [];
    }
}

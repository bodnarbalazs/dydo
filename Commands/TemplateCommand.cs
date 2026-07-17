namespace DynaDocs.Commands;

using System.CommandLine;
using System.Security.Cryptography;
using System.Text;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class TemplateCommand
{
    // Framework-owned files relative to the dydo root — derived from role definitions
    public static readonly string[] FrameworkTemplateFiles =
        TemplateGenerator.GetAllTemplateNames()
            .Select(name => $"_system/templates/{name}")
            .ToArray();

    public static readonly string[] FrameworkDocFiles =
    [
        "reference/about-dynadocs.md",
        "reference/dydo-commands.md",
        "reference/writing-docs.md",
        "guides/how-to-use-docs.md"
    ];

    public static readonly string[] FrameworkBinaryFiles =
    [
        "_assets/dydo-diagram.svg"
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
        var forceOption = new Option<bool>("--force") { Description = "Overwrite even if re-anchoring fails (backs up first)" };

        var command = new Command("update", "Update framework templates and docs");
        command.Options.Add(diffOption);
        command.Options.Add(forceOption);

        command.SetAction(parseResult =>
        {
            var diff = parseResult.GetValue(diffOption);
            var force = parseResult.GetValue(forceOption);
            return ExecuteUpdate(diff, force);
        });

        return command;
    }

    private static int ExecuteUpdate(bool diff, bool force)
    {
        var configService = new ConfigService();
        var configPath = configService.FindConfigFile();
        if (configPath == null)
        {
            Console.Error.WriteLine("No dydo.json found. Run 'dydo init' first.");
            return 1;
        }

        var config = configService.LoadConfig()!;
        var dydoRoot = configService.GetDydoRoot();
        MigrateHashFormat(config, dydoRoot);

        var tally = new UpdateTally();
        UpdateFrameworkFiles(dydoRoot, config, diff, force, tally);

        tally.Updated += CleanStaleTemplates(dydoRoot, config, diff);
        PruneStaleHashes(config, diff);

        tally.Updated += ApplyConfigDefaults(config, diff);
        tally.Updated += EnsureTypesJson(dydoRoot, diff);

        if (!diff)
            configService.SaveConfig(config, configPath);

        ReportSummary(tally);

        return tally.Warnings.Count > 0 && !force ? 1 : 0;
    }

    private static void UpdateFrameworkFiles(
        string dydoRoot, DydoConfig config, bool diff, bool force, UpdateTally tally)
    {
        foreach (var relativePath in FrameworkTemplateFiles)
            AccumulateResult(UpdateTemplateFile(relativePath, dydoRoot, config, diff, force),
                tally, forceCountWarning: force);

        foreach (var relativePath in FrameworkDocFiles)
            AccumulateResult(UpdateDocFile(relativePath, dydoRoot, config, diff), tally);

        foreach (var relativePath in FrameworkBinaryFiles)
            AccumulateResult(UpdateBinaryFile(relativePath, dydoRoot, config, diff), tally);
    }

    private static int ApplyConfigDefaults(DydoConfig config, bool diff)
    {
        var updated = 0;

        var nudgesAdded = diff ? 0 : ConfigFactory.EnsureDefaultNudges(config);
        if (nudgesAdded > 0)
        {
            Console.WriteLine($"  Added {nudgesAdded} default nudge(s)");
            updated += nudgesAdded;
        }


        var modelsUpgraded = !diff && ConfigFactory.UpgradeLegacyOpenAiTierDefaults(config);
        if (modelsUpgraded)
        {
            Console.WriteLine("  Upgraded legacy OpenAI model defaults");
            updated++;
        }

        updated += EnsureScanExcludeWithReport(config, diff);
        return updated;
    }

    private static void ReportSummary(UpdateTally tally)
    {
        var summary = $"Template update complete: {tally.Updated} updated, {tally.Skipped} already current";
        if (tally.Warned > 0)
            summary += $", {tally.Warned} warned";
        Console.WriteLine(summary + ".");

        foreach (var warning in tally.Warnings)
            Console.Error.WriteLine($"  Warning: {warning}");
    }

    private static void AccumulateResult(
        UpdateResult result, UpdateTally tally, bool forceCountWarning = false)
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
                if (forceCountWarning) tally.Updated++;
                break;
        }
    }

    private static int CleanStaleTemplates(string dydoRoot, DydoConfig config, bool diff)
    {
        var validSet = new HashSet<string>(FrameworkTemplateFiles);
        var templatesDir = Path.Combine(dydoRoot, "_system", "templates");
        if (!Directory.Exists(templatesDir))
            return 0;

        var removed = 0;
        foreach (var file in Directory.GetFiles(templatesDir, "*.template.md"))
        {
            var relative = "_system/templates/" + Path.GetFileName(file);
            if (validSet.Contains(relative)) continue;

            // Only remove files we know we own (hash-tracked framework copies that are no
            // longer shipped). An untracked mode-*.template.md is a user's custom role —
            // dydo sync compiles it — and any other untracked template is user data too.
            if (!config.FrameworkHashes.ContainsKey(relative)) continue;

            if (!diff)
                File.Delete(file);
            Console.WriteLine($"  Removed stale: {relative}");
            removed++;
        }
        return removed;
    }

    private static void PruneStaleHashes(DydoConfig config, bool diff)
    {
        var validKeys = new HashSet<string>(FrameworkTemplateFiles
            .Concat(FrameworkDocFiles)
            .Concat(FrameworkBinaryFiles));
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

    private static int EnsureScanExcludeWithReport(DydoConfig config, bool diff)
    {
        if (diff) return 0;

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

    private static UpdateResult UpdateTemplateFile(
        string relativePath, string dydoRoot, DydoConfig config, bool diff, bool force)
    {
        var fullPath = Path.Combine(dydoRoot, relativePath);
        var templateName = Path.GetFileName(relativePath);
        var embeddedContent = TemplateGenerator.ReadBuiltInTemplate(templateName);

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
        var isUserEdited = storedHash != null ? storedHash != onDiskHash : true;

        if (!isUserEdited)
            return WriteUpdate(fullPath, relativePath, embeddedContent, config, diff);

        return HandleUserEditedTemplate(
            fullPath, relativePath, embeddedContent, onDisk, config, diff, force);
    }

    private static UpdateResult HandleUserEditedTemplate(
        string fullPath, string relativePath, string embeddedContent,
        string onDisk, DydoConfig config, bool diff, bool force)
    {
        var oldStock = GetOldStockContent(relativePath, config, onDisk, embeddedContent);
        var userIncludes = IncludeReanchor.ExtractUserIncludes(oldStock, onDisk);

        if (userIncludes.Count == 0)
        {
            if (!diff)
            {
                File.WriteAllText(fullPath, embeddedContent);
                config.FrameworkHashes[relativePath] = ComputeHash(embeddedContent);
            }
            Console.WriteLine($"  Updated: {relativePath} (user edits were non-include changes, overwritten)");
            return new UpdateResult.Updated();
        }

        var reanchorResult = IncludeReanchor.Reanchor(embeddedContent, userIncludes);

        if (diff)
            return ReportReanchorDiff(relativePath, reanchorResult);

        if (reanchorResult.Unplaced.Count > 0 && !force)
            return HandleUnplacedTags(fullPath, relativePath, reanchorResult);

        if (reanchorResult.Unplaced.Count > 0)
            BackupAndSaveUnplaced(fullPath, relativePath, reanchorResult);

        File.WriteAllText(fullPath, reanchorResult.Content);
        config.FrameworkHashes[relativePath] = ComputeHash(reanchorResult.Content);

        foreach (var tag in reanchorResult.Placed)
            Console.WriteLine($"    Re-anchored: {tag}");
        foreach (var tag in reanchorResult.Unplaced)
            Console.Error.WriteLine($"    UNPLACED: {tag}");

        Console.WriteLine($"  Updated: {relativePath}");
        return new UpdateResult.Updated();
    }

    private static UpdateResult ReportReanchorDiff(
        string relativePath, IncludeReanchor.ReanchorResult result)
    {
        Console.WriteLine($"  Would update: {relativePath}");
        foreach (var tag in result.Placed)
            Console.WriteLine($"    Re-anchor: {tag}");
        foreach (var tag in result.Unplaced)
            Console.WriteLine($"    UNPLACED: {tag}");
        return result.Unplaced.Count > 0
            ? new UpdateResult.Warning($"{relativePath}: {result.Unplaced.Count} tag(s) could not be re-anchored")
            : new UpdateResult.Updated();
    }

    private static UpdateResult HandleUnplacedTags(
        string fullPath, string relativePath, IncludeReanchor.ReanchorResult result)
    {
        Console.Error.WriteLine($"  Skipped: {relativePath} — {result.Unplaced.Count} tag(s) could not be re-anchored. Use --force to override.");
        var unplacedPath = fullPath + ".unplaced";
        File.WriteAllText(unplacedPath, string.Join("\n", result.Unplaced));
        return new UpdateResult.Warning($"{relativePath}: unplaced tags saved to {Path.GetFileName(unplacedPath)}");
    }

    private static void BackupAndSaveUnplaced(
        string fullPath, string relativePath, IncludeReanchor.ReanchorResult result)
    {
        var backupPath = fullPath + ".backup";
        File.Copy(fullPath, backupPath, overwrite: true);
        Console.WriteLine($"  Backed up: {relativePath} -> {Path.GetFileName(backupPath)}");

        var unplacedPath = fullPath + ".unplaced";
        File.WriteAllText(unplacedPath, string.Join("\n", result.Unplaced));
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

    private static UpdateResult UpdateBinaryFile(
        string relativePath, string dydoRoot, DydoConfig config, bool diff)
    {
        var fullPath = Path.Combine(dydoRoot, relativePath);
        var fileName = Path.GetFileName(relativePath);
        var embeddedBytes = TemplateGenerator.ReadEmbeddedAsset(fileName);
        if (embeddedBytes == null)
            return new UpdateResult.Skipped();

        if (!File.Exists(fullPath))
        {
            if (!diff)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                File.WriteAllBytes(fullPath, embeddedBytes);
                config.FrameworkHashes[relativePath] = ComputeHashBytes(embeddedBytes);
            }
            Console.WriteLine($"  Created: {relativePath}");
            return new UpdateResult.Updated();
        }

        var onDiskBytes = File.ReadAllBytes(fullPath);
        var embeddedHash = ComputeHashBytes(embeddedBytes);
        var onDiskHash = ComputeHashBytes(onDiskBytes);

        if (onDiskHash == embeddedHash)
        {
            config.FrameworkHashes[relativePath] = embeddedHash;
            return new UpdateResult.Skipped();
        }

        var storedHash = config.FrameworkHashes.GetValueOrDefault(relativePath);

        if (storedHash != null && storedHash != onDiskHash)
        {
            Console.Error.WriteLine($"  Skipped: {relativePath} — user-edited (hash mismatch)");
            return new UpdateResult.Warning($"{relativePath}: user-edited, skipped");
        }

        if (!diff)
        {
            File.WriteAllBytes(fullPath, embeddedBytes);
            config.FrameworkHashes[relativePath] = embeddedHash;
        }
        Console.WriteLine($"  Updated: {relativePath}");
        return new UpdateResult.Updated();
    }

    private static string? GetEmbeddedDocContent(string relativePath) => relativePath switch
    {
        "reference/about-dynadocs.md" => TemplateGenerator.GenerateAboutDynadocsMd(),
        "reference/dydo-commands.md" => TemplateGenerator.GenerateDydoCommandsMd(),
        "reference/writing-docs.md" => TemplateGenerator.GenerateWritingDocsMd(),
        "guides/how-to-use-docs.md" => TemplateGenerator.GenerateHowToUseDocsMd(),
        _ => null
    };

    private static string GetOldStockContent(
        string relativePath, DydoConfig config, string onDisk, string embeddedContent)
    {
        var storedHash = config.FrameworkHashes.GetValueOrDefault(relativePath);
        if (storedHash == null)
            return onDisk;

        if (ComputeHash(embeddedContent) == storedHash)
            return embeddedContent;

        // Both user and framework changed — old stock irrecoverable
        return onDisk;
    }

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

    /// <summary>
    /// Migrates stored hashes from pre-normalization format to normalized format.
    /// Safe to call on every update — no-ops when hashes are already current.
    /// </summary>
    public static void MigrateHashFormat(DydoConfig config, string dydoRoot)
    {
        foreach (var relativePath in config.FrameworkHashes.Keys.ToList())
        {
            var fullPath = Path.Combine(dydoRoot, relativePath);
            if (!File.Exists(fullPath)) continue;

            var onDisk = File.ReadAllText(fullPath);
            var normalizedHash = ComputeHash(onDisk);
            var storedHash = config.FrameworkHashes[relativePath];
            if (storedHash == normalizedHash) continue;

            // Check if stored hash matches raw (un-normalized) content
            var rawHash = Convert.ToHexStringLower(
                SHA256.HashData(Encoding.UTF8.GetBytes(onDisk)));
            if (storedHash == rawHash)
                config.FrameworkHashes[relativePath] = normalizedHash;
        }
    }

    private abstract record UpdateResult
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
        public List<string> Warnings { get; } = [];
    }
}

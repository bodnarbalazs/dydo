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
        if (PathUtils.IsInsideWorktree())
        {
            Console.Error.WriteLine("Cannot update templates inside a worktree. Run from the main project directory.");
            return 1;
        }

        var configService = new ConfigService();
        var configPath = configService.FindConfigFile();
        if (configPath == null)
        {
            Console.Error.WriteLine("No dydo.json found. Run 'dydo init' first.");
            return 1;
        }

        var config = configService.LoadConfig()!;
        var dydoRoot = configService.GetDydoRoot();
        var updated = 0;
        var skipped = 0;
        var warnings = new List<string>();

        foreach (var relativePath in FrameworkTemplateFiles)
            AccumulateResult(UpdateTemplateFile(relativePath, dydoRoot, config, diff, force),
                ref updated, ref skipped, warnings, forceCountWarning: force);

        foreach (var relativePath in FrameworkDocFiles)
            AccumulateResult(UpdateDocFile(relativePath, dydoRoot, config, diff),
                ref updated, ref skipped, warnings);

        foreach (var relativePath in FrameworkBinaryFiles)
            AccumulateResult(UpdateBinaryFile(relativePath, dydoRoot, config, diff),
                ref updated, ref skipped, warnings);

        updated += CleanStaleTemplates(dydoRoot, diff);
        PruneStaleHashes(config, diff);
        RegenerateAgentWorkspaces(dydoRoot, config, diff);

        if (!diff)
            configService.SaveConfig(config, configPath);

        Console.WriteLine($"Template update complete: {updated} updated, {skipped} already current.");

        foreach (var warning in warnings)
            Console.Error.WriteLine($"  Warning: {warning}");

        return warnings.Count > 0 && !force ? 1 : 0;
    }

    private static void AccumulateResult(UpdateResult result,
        ref int updated, ref int skipped, List<string> warnings, bool forceCountWarning = false)
    {
        switch (result)
        {
            case UpdateResult.Updated:
                updated++;
                break;
            case UpdateResult.Skipped:
                skipped++;
                break;
            case UpdateResult.Warning warning:
                warnings.Add(warning.Message);
                if (forceCountWarning) updated++;
                break;
        }
    }

    private static int CleanStaleTemplates(string dydoRoot, bool diff)
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

    private static void RegenerateAgentWorkspaces(string dydoRoot, DydoConfig config, bool diff)
    {
        var agentsPath = Path.Combine(dydoRoot, "agents");
        if (!Directory.Exists(agentsPath))
            return;

        var scaffolder = new FolderScaffolder();
        var sourcePaths = config.Paths.Source;
        var testPaths = config.Paths.Tests;
        foreach (var agentDir in Directory.GetDirectories(agentsPath))
        {
            var agentName = Path.GetFileName(agentDir);
            if (!diff)
                scaffolder.RegenerateAgentFiles(agentsPath, agentName, sourcePaths, testPaths);
            Console.WriteLine($"  Regenerated: agents/{agentName}");
        }
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

        if (onDisk == embeddedContent)
        {
            config.FrameworkHashes[relativePath] = ComputeHash(embeddedContent);
            return new UpdateResult.Skipped();
        }

        var storedHash = config.FrameworkHashes.GetValueOrDefault(relativePath);
        var onDiskHash = ComputeHash(onDisk);
        var isUserEdited = storedHash != null ? storedHash != onDiskHash : onDisk != embeddedContent;

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
        if (onDisk == embeddedContent)
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
            return embeddedContent;

        return ComputeHash(onDisk) == storedHash ? onDisk : embeddedContent;
    }

    public static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexStringLower(bytes);
    }

    public static string ComputeHashBytes(byte[] content)
    {
        var bytes = SHA256.HashData(content);
        return Convert.ToHexStringLower(bytes);
    }

    private abstract record UpdateResult
    {
        public sealed record Updated : UpdateResult;
        public sealed record Skipped : UpdateResult;
        public sealed record Warning(string Message) : UpdateResult;
    }
}

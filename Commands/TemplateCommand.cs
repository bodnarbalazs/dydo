namespace DynaDocs.Commands;

using System.CommandLine;
using System.Security.Cryptography;
using System.Text;
using DynaDocs.Models;
using DynaDocs.Services;

public static class TemplateCommand
{
    // Framework-owned files relative to the dydo root
    public static readonly string[] FrameworkTemplateFiles =
    [
        "_system/templates/agent-workflow.template.md",
        "_system/templates/mode-code-writer.template.md",
        "_system/templates/mode-reviewer.template.md",
        "_system/templates/mode-co-thinker.template.md",
        "_system/templates/mode-interviewer.template.md",
        "_system/templates/mode-planner.template.md",
        "_system/templates/mode-docs-writer.template.md",
        "_system/templates/mode-tester.template.md"
    ];

    public static readonly string[] FrameworkDocFiles =
    [
        "reference/about-dynadocs.md",
        "reference/dydo-commands.md",
        "reference/writing-docs.md",
        "guides/how-to-use-docs.md"
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
        var updated = 0;
        var skipped = 0;
        var warnings = new List<string>();

        foreach (var relativePath in FrameworkTemplateFiles)
        {
            var result = UpdateTemplateFile(relativePath, dydoRoot, config, diff, force);
            switch (result)
            {
                case UpdateResult.Updated: updated++; break;
                case UpdateResult.Skipped: skipped++; break;
                case UpdateResult.Warning warning:
                    warnings.Add(warning.Message);
                    if (force) updated++;
                    break;
            }
        }

        foreach (var relativePath in FrameworkDocFiles)
        {
            var result = UpdateDocFile(relativePath, dydoRoot, config, diff);
            switch (result)
            {
                case UpdateResult.Updated: updated++; break;
                case UpdateResult.Skipped: skipped++; break;
                case UpdateResult.Warning warning:
                    warnings.Add(warning.Message);
                    break;
            }
        }

        if (!diff)
        {
            configService.SaveConfig(config, configPath);
        }

        Console.WriteLine($"Template update complete: {updated} updated, {skipped} already current.");

        foreach (var warning in warnings)
            Console.Error.WriteLine($"  Warning: {warning}");

        return warnings.Count > 0 && !force ? 1 : 0;
    }

    private static UpdateResult UpdateTemplateFile(
        string relativePath, string dydoRoot, DydoConfig config, bool diff, bool force)
    {
        var fullPath = Path.Combine(dydoRoot, relativePath);
        var templateName = Path.GetFileName(relativePath);
        var embeddedContent = TemplateGenerator.ReadBuiltInTemplate(templateName);

        if (!File.Exists(fullPath))
        {
            if (!diff)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                File.WriteAllText(fullPath, embeddedContent);
                config.FrameworkHashes[relativePath] = ComputeHash(embeddedContent);
            }
            Console.WriteLine($"  Created: {relativePath}");
            return new UpdateResult.Updated();
        }

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
        {
            if (!diff)
            {
                File.WriteAllText(fullPath, embeddedContent);
                config.FrameworkHashes[relativePath] = ComputeHash(embeddedContent);
            }
            Console.WriteLine($"  Updated: {relativePath}");
            return new UpdateResult.Updated();
        }

        // User edited — extract user-added includes and re-anchor
        var oldStock = storedHash != null && storedHash == onDiskHash
            ? onDisk
            : GetOldStockContent(relativePath, config, onDisk, embeddedContent);

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
        {
            Console.WriteLine($"  Would update: {relativePath}");
            foreach (var tag in reanchorResult.Placed)
                Console.WriteLine($"    Re-anchor: {tag}");
            foreach (var tag in reanchorResult.Unplaced)
                Console.WriteLine($"    UNPLACED: {tag}");
            return reanchorResult.Unplaced.Count > 0
                ? new UpdateResult.Warning($"{relativePath}: {reanchorResult.Unplaced.Count} tag(s) could not be re-anchored")
                : new UpdateResult.Updated();
        }

        if (reanchorResult.Unplaced.Count > 0 && !force)
        {
            Console.Error.WriteLine($"  Skipped: {relativePath} — {reanchorResult.Unplaced.Count} tag(s) could not be re-anchored. Use --force to override.");
            var unplacedPath = fullPath + ".unplaced";
            File.WriteAllText(unplacedPath, string.Join("\n", reanchorResult.Unplaced));
            return new UpdateResult.Warning($"{relativePath}: unplaced tags saved to {Path.GetFileName(unplacedPath)}");
        }

        if (reanchorResult.Unplaced.Count > 0 && force)
        {
            var backupPath = fullPath + ".backup";
            File.Copy(fullPath, backupPath, overwrite: true);
            Console.WriteLine($"  Backed up: {relativePath} -> {Path.GetFileName(backupPath)}");

            var unplacedPath = fullPath + ".unplaced";
            File.WriteAllText(unplacedPath, string.Join("\n", reanchorResult.Unplaced));
        }

        File.WriteAllText(fullPath, reanchorResult.Content);
        config.FrameworkHashes[relativePath] = ComputeHash(reanchorResult.Content);

        foreach (var tag in reanchorResult.Placed)
            Console.WriteLine($"    Re-anchored: {tag}");
        foreach (var tag in reanchorResult.Unplaced)
            Console.Error.WriteLine($"    UNPLACED: {tag}");

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
        {
            if (!diff)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                File.WriteAllText(fullPath, embeddedContent);
                config.FrameworkHashes[relativePath] = ComputeHash(embeddedContent);
            }
            Console.WriteLine($"  Created: {relativePath}");
            return new UpdateResult.Updated();
        }

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

        if (!diff)
        {
            File.WriteAllText(fullPath, embeddedContent);
            config.FrameworkHashes[relativePath] = ComputeHash(embeddedContent);
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
        // If no stored hash, the on-disk version is our best reference for diffing
        // If stored hash matches on-disk, on-disk IS the old stock
        var storedHash = config.FrameworkHashes.GetValueOrDefault(relativePath);
        if (storedHash == null)
            return embeddedContent;

        return ComputeHash(onDisk) == storedHash ? onDisk : embeddedContent;
    }

    internal static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexStringLower(bytes);
    }

    private abstract record UpdateResult
    {
        public sealed record Updated : UpdateResult;
        public sealed record Skipped : UpdateResult;
        public sealed record Warning(string Message) : UpdateResult;
    }
}

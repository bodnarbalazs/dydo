namespace DynaDocs.Tests.Commands;

using System.CommandLine;
using System.Reflection;
using System.Text.RegularExpressions;
using DynaDocs.Commands;

/// <summary>
/// Meta-tests that dynamically discover commands/flags from the System.CommandLine tree
/// and verify they appear in docs, help text, templates, and examples.
/// </summary>
public class CommandDocConsistencyTests
{
    private static readonly HashSet<string> ExcludedFromDocs = ["_complete"];
    // Commands intentionally hidden from agent-facing quick reference
    private static readonly HashSet<string> ExcludedPaths = ["guard lift", "guard restore", "completions"];
    private static readonly HashSet<string> BuiltInOptionNames = ["--help", "-h", "-?", "--version"];

    private static RootCommand BuildRootCommand()
    {
        var root = new RootCommand("Test");
        root.Subcommands.Add(CheckCommand.Create());
        root.Subcommands.Add(FixCommand.Create());
        root.Subcommands.Add(IndexCommand.Create());
        root.Subcommands.Add(InitCommand.Create());
        root.Subcommands.Add(GraphCommand.Create());
        root.Subcommands.Add(AgentCommand.Create());
        root.Subcommands.Add(GuardCommand.Create());
        root.Subcommands.Add(DispatchCommand.Create());
        root.Subcommands.Add(InboxCommand.Create());
        root.Subcommands.Add(TaskCommand.Create());
        root.Subcommands.Add(ReviewCommand.Create());
        root.Subcommands.Add(WorkspaceCommand.Create());
        root.Subcommands.Add(WhoamiCommand.Create());
        root.Subcommands.Add(AuditCommand.Create());
        root.Subcommands.Add(CompletionsCommand.Create());
        root.Subcommands.Add(CompleteCommand.Create());
        root.Subcommands.Add(TemplateCommand.Create());
        return root;
    }

    private static List<(string Path, Command Cmd)> GetAllCommands()
    {
        var root = BuildRootCommand();
        return WalkCommands(root).ToList();
    }

    private static List<(string Path, Command Cmd)> GetDocumentedCommands() =>
        GetAllCommands().Where(c => !ExcludedFromDocs.Contains(c.Cmd.Name) && !ExcludedPaths.Contains(c.Path)).ToList();

    private static IEnumerable<(string Path, Command Cmd)> WalkCommands(Command root, string prefix = "")
    {
        foreach (var sub in root.Subcommands)
        {
            var path = string.IsNullOrEmpty(prefix) ? sub.Name : $"{prefix} {sub.Name}";
            yield return (path, sub);
            foreach (var child in WalkCommands(sub, path))
                yield return child;
        }
    }

    private static string FindRepoFile(string relativePath)
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            var candidate = Path.Combine(dir, relativePath);
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        throw new FileNotFoundException(
            $"Could not find {relativePath} walking up from {AppContext.BaseDirectory}");
    }

    private static string FindRepoDir(string relativePath)
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            var candidate = Path.Combine(dir, relativePath);
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        throw new DirectoryNotFoundException(
            $"Could not find {relativePath} walking up from {AppContext.BaseDirectory}");
    }

    private static HashSet<string> GetAllNames(Option opt)
    {
        var names = new HashSet<string>(opt.Aliases) { opt.Name };
        return names;
    }

    private static List<Option> GetUserOptions(Command cmd) =>
        cmd.Options.Where(o => !GetAllNames(o).Any(BuiltInOptionNames.Contains)).ToList();

    /// <summary>
    /// Extract sections keyed by command path from a reference doc.
    /// Sections start with "### dydo &lt;path&gt;" and end at the next "### " or "---" or "## ".
    /// </summary>
    private static Dictionary<string, string> ExtractDocSections(string content)
    {
        var sections = new Dictionary<string, string>();
        var headerPattern = new Regex(@"^### dydo (.+?)$", RegexOptions.Multiline);
        var matches = headerPattern.Matches(content);

        for (int i = 0; i < matches.Count; i++)
        {
            var name = matches[i].Groups[1].Value.Trim();
            var start = matches[i].Index + matches[i].Length;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : content.Length;

            var nextBoundary = Regex.Match(content[start..end], @"^(---|## )", RegexOptions.Multiline);
            if (nextBoundary.Success)
                end = start + nextBoundary.Index;

            sections[name] = content[start..end];
        }

        return sections;
    }

    private static HashSet<string> ExtractFlags(string text)
    {
        var pattern = new Regex(@"`(--[\w-]+)`");
        return pattern.Matches(text).Select(m => m.Groups[1].Value).ToHashSet();
    }

    // ──────────────────────────────────────────────
    // Test 1: Every registered command appears in help text
    // ──────────────────────────────────────────────

    [Fact]
    public void AllCommands_AppearInHelpText()
    {
        var commands = GetDocumentedCommands();
        var programCs = File.ReadAllText(FindRepoFile("Program.cs"));

        var missing = commands
            .Where(c => !programCs.Contains(c.Path))
            .Select(c => c.Path)
            .ToList();

        Assert.True(missing.Count == 0,
            $"Commands missing from help text in Program.cs:\n  {string.Join("\n  ", missing)}");
    }

    // ──────────────────────────────────────────────
    // Test 2: Every command option appears in the reference doc
    // ──────────────────────────────────────────────

    [Fact]
    public void AllOptions_AppearInReferenceDoc()
    {
        var commands = GetDocumentedCommands();
        var refDoc = File.ReadAllText(FindRepoFile(Path.Combine("dydo", "reference", "dydo-commands.md")));
        var sections = ExtractDocSections(refDoc);

        var missing = new List<string>();
        foreach (var (path, cmd) in commands)
        {
            var options = GetUserOptions(cmd);
            if (options.Count == 0) continue;

            if (!sections.TryGetValue(path, out var section))
            {
                missing.Add($"Section '### dydo {path}' missing (has options: {string.Join(", ", options.Select(o => o.Name))})");
                continue;
            }

            foreach (var opt in options)
            {
                var found = GetAllNames(opt).Any(name => section.Contains(name));
                if (!found)
                    missing.Add($"{opt.Name} (command: {path})");
            }
        }

        Assert.True(missing.Count == 0,
            $"Options missing from dydo-commands.md:\n  {string.Join("\n  ", missing)}");
    }

    // ──────────────────────────────────────────────
    // Test 3: Reference doc and template have same options per command
    // ──────────────────────────────────────────────

    [Fact]
    public void ReferenceDocAndTemplate_HaveSameOptions()
    {
        var refDoc = File.ReadAllText(FindRepoFile(Path.Combine("dydo", "reference", "dydo-commands.md")));
        var template = File.ReadAllText(FindRepoFile(Path.Combine("Templates", "dydo-commands.template.md")));

        var refSections = ExtractDocSections(refDoc);
        var tmplSections = ExtractDocSections(template);

        var allSectionNames = refSections.Keys.Union(tmplSections.Keys).ToHashSet();
        var mismatches = new List<string>();

        foreach (var name in allSectionNames)
        {
            var inRef = refSections.ContainsKey(name);
            var inTmpl = tmplSections.ContainsKey(name);

            if (inRef && !inTmpl)
            {
                mismatches.Add($"'### dydo {name}' in reference but not template");
                continue;
            }
            if (!inRef && inTmpl)
            {
                mismatches.Add($"'### dydo {name}' in template but not reference");
                continue;
            }

            var refFlags = ExtractFlags(refSections[name]);
            var tmplFlags = ExtractFlags(tmplSections[name]);

            var onlyInRef = refFlags.Except(tmplFlags).ToList();
            var onlyInTmpl = tmplFlags.Except(refFlags).ToList();

            foreach (var flag in onlyInRef)
                mismatches.Add($"{flag} (dydo {name}): in reference but not template");
            foreach (var flag in onlyInTmpl)
                mismatches.Add($"{flag} (dydo {name}): in template but not reference");
        }

        Assert.True(mismatches.Count == 0,
            $"Mismatches between dydo-commands.md and template:\n  {string.Join("\n  ", mismatches)}");
    }

    // ──────────────────────────────────────────────
    // Test 4: Examples include all required flags
    // ──────────────────────────────────────────────

    [Fact]
    public void Examples_IncludeAllRequiredFlags()
    {
        var commands = GetDocumentedCommands();
        var refDoc = File.ReadAllText(FindRepoFile(Path.Combine("dydo", "reference", "dydo-commands.md")));
        var sections = ExtractDocSections(refDoc);

        var commandPaths = commands.Select(c => c.Path).ToHashSet();
        var missing = new List<string>();

        foreach (var (path, cmd) in commands)
        {
            var requiredOptions = GetUserOptions(cmd).Where(o => o.Required).ToList();
            if (requiredOptions.Count == 0) continue;
            if (!sections.TryGetValue(path, out var section)) continue;

            var codeBlockPattern = new Regex(@"```[\w]*\r?\n(.*?)```", RegexOptions.Singleline);
            var exampleLines = new List<string>();
            foreach (Match block in codeBlockPattern.Matches(section))
            {
                foreach (var line in block.Groups[1].Value.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith('#')) continue;
                    if (trimmed.StartsWith("dydo "))
                        exampleLines.Add(trimmed);
                }
            }

            foreach (var opt in requiredOptions)
            {
                var foundInAnyExample = exampleLines.Any(line =>
                    GetAllNames(opt).Any(name => line.Contains(name)));

                if (!foundInAnyExample && exampleLines.Count > 0)
                    missing.Add($"{opt.Name} (command: dydo {path}) not in any example");
            }
        }

        Assert.True(missing.Count == 0,
            $"Required flags missing from examples in dydo-commands.md:\n  {string.Join("\n  ", missing)}");
    }

    // ──────────────────────────────────────────────
    // Test 5: All command factories appear in smoke tests
    // ──────────────────────────────────────────────

    [Fact]
    public void AllCommandFactories_InSmokeTests()
    {
        var assembly = typeof(CheckCommand).Assembly;
        var commandFactories = assembly.GetTypes()
            .Where(t => t.Namespace == "DynaDocs.Commands"
                && t.IsClass && t.IsPublic && t.IsAbstract && t.IsSealed) // static class
            .Where(t => t.GetMethod("Create", BindingFlags.Public | BindingFlags.Static) != null)
            .Select(t => t.Name)
            .ToList();

        var smokeTestFile = File.ReadAllText(FindRepoFile(
            Path.Combine("DynaDocs.Tests", "Commands", "CommandSmokeTests.cs")));

        var missing = commandFactories
            .Where(name => !smokeTestFile.Contains(name))
            .ToList();

        Assert.True(missing.Count == 0,
            $"Command factories missing from CommandSmokeTests.cs:\n  {string.Join("\n  ", missing)}");
    }

    // ──────────────────────────────────────────────
    // Test 6: About-dynadocs quick reference includes all commands
    // ──────────────────────────────────────────────

    [Fact]
    public void AboutQuickReference_IncludesAllCommands()
    {
        var commands = GetDocumentedCommands();
        var filesToCheck = new[]
        {
            Path.Combine("dydo", "reference", "about-dynadocs.md"),
            Path.Combine("Templates", "about-dynadocs.template.md")
        };

        var missing = new List<string>();
        foreach (var file in filesToCheck)
        {
            var content = File.ReadAllText(FindRepoFile(file));
            var refIdx = content.IndexOf("## Command Reference");
            if (refIdx < 0)
            {
                missing.Add($"{file}: '## Command Reference' section not found");
                continue;
            }
            var refSection = content[refIdx..];

            foreach (var (path, cmd) in commands)
            {
                // Only check leaf commands (commands without subcommands)
                // Parent commands match as substrings of their children
                if (cmd.Subcommands.Any()) continue;

                if (!refSection.Contains($"dydo {path}"))
                    missing.Add($"{file}: missing 'dydo {path}'");
            }
        }

        Assert.True(missing.Count == 0,
            $"Commands missing from quick reference:\n  {string.Join("\n  ", missing)}");
    }

    // ──────────────────────────────────────────────
    // Test 7: Template/mode examples use required flags
    // ──────────────────────────────────────────────

    [Fact]
    public void TemplateExamples_UseRequiredFlags()
    {
        var commands = GetDocumentedCommands();
        var commandLookup = commands.ToDictionary(c => c.Path, c => c.Cmd);
        var commandPaths = commands.Select(c => c.Path).OrderByDescending(p => p.Length).ToList();

        var files = new List<string>();

        // Template files
        var templatesDir = FindRepoDir("Templates");
        files.AddRange(Directory.GetFiles(templatesDir, "*.template.md"));

        // Override templates in _system/templates/ (canonical source for generated mode files)
        try
        {
            var systemTemplatesDir = FindRepoDir(Path.Combine("dydo", "_system", "templates"));
            files.AddRange(Directory.GetFiles(systemTemplatesDir, "*.template.md"));
        }
        catch (DirectoryNotFoundException) { }

        var codeBlockPattern = new Regex(@"```[\w]*\r?\n(.*?)```", RegexOptions.Singleline);
        var missing = new List<string>();

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            var fileName = Path.GetFileName(file);

            foreach (Match block in codeBlockPattern.Matches(content))
            {
                foreach (var line in block.Groups[1].Value.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (!trimmed.StartsWith("dydo ")) continue;
                    if (trimmed.StartsWith('#')) continue;

                    // Identify which command this example invokes
                    var cmdPath = IdentifyCommand(trimmed, commandPaths);
                    if (cmdPath == null || !commandLookup.TryGetValue(cmdPath, out var cmd)) continue;

                    var requiredOptions = GetUserOptions(cmd).Where(o => o.Required).ToList();
                    foreach (var opt in requiredOptions)
                    {
                        var hasFlag = GetAllNames(opt).Any(name => trimmed.Contains(name));
                        // Allow <placeholder> syntax for the flag name
                        var hasPlaceholder = trimmed.Contains($"<{opt.Name.TrimStart('-')}>");
                        if (!hasFlag && !hasPlaceholder)
                            missing.Add($"{fileName}: 'dydo {cmdPath}' example missing required {opt.Name}");
                    }
                }
            }
        }

        var unique = missing.Distinct().ToList();
        Assert.True(unique.Count == 0,
            $"Required flags missing from template/mode examples:\n  {string.Join("\n  ", unique)}");
    }

    // ──────────────────────────────────────────────
    // Test 8: License section is consistent across all README-like files
    // ──────────────────────────────────────────────

    [Fact]
    public void LicenseSection_ConsistentAcrossAllReadmes()
    {
        var files = new[]
        {
            "README.md",
            Path.Combine("npm", "README.md"),
            Path.Combine("Templates", "about-dynadocs.template.md"),
            Path.Combine("dydo", "reference", "about-dynadocs.md"),
        };

        var mainLicense = ExtractSection(File.ReadAllText(FindRepoFile(files[0])), "## License");
        Assert.False(string.IsNullOrEmpty(mainLicense),
            "README.md is missing '## License' section");

        var mismatches = new List<string>();
        foreach (var file in files.Skip(1))
        {
            var content = File.ReadAllText(FindRepoFile(file));
            var license = ExtractSection(content, "## License");

            if (string.IsNullOrEmpty(license))
                mismatches.Add($"{file}: missing '## License' section");
            else if (license != mainLicense)
                mismatches.Add($"{file}: License section differs from README.md\n" +
                    $"  Expected:\n{mainLicense}\n  Actual:\n{license}");
        }

        Assert.True(mismatches.Count == 0,
            $"License section inconsistencies (README.md is source of truth):\n\n{string.Join("\n\n", mismatches)}");
    }

    /// <summary>
    /// Extract a section from markdown starting at the given heading and ending
    /// at the next heading of the same or higher level, or end of file.
    /// </summary>
    private static string ExtractSection(string content, string heading)
    {
        var headingLevel = heading.TakeWhile(c => c == '#').Count();
        var idx = content.IndexOf(heading, StringComparison.Ordinal);
        if (idx < 0) return "";

        var afterHeading = idx + heading.Length;
        var boundary = new Regex($@"^#{{{1},{headingLevel}}}\s", RegexOptions.Multiline);
        var match = boundary.Match(content, afterHeading);
        var end = match.Success ? match.Index : content.Length;

        return content[idx..end].ReplaceLineEndings("\n").TrimEnd();
    }

    /// <summary>
    /// Given a "dydo ..." line, find the longest matching command path.
    /// </summary>
    private static string? IdentifyCommand(string line, List<string> commandPaths)
    {
        // Remove "dydo " prefix
        var rest = line["dydo ".Length..];

        foreach (var path in commandPaths) // already sorted longest-first
        {
            if (rest.StartsWith(path + " ") || rest == path)
                return path;
        }

        // Try single-word match
        var firstWord = rest.Split(' ')[0];
        return commandPaths.Contains(firstWord) ? firstWord : null;
    }
}

namespace DynaDocs.Commands;

using System.Text.RegularExpressions;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

internal static partial class IssueCreateHandler
{
    public static int Execute(string title, string area, string severity, string? foundBy, string? body = null, string? bodyFile = null)
    {
        if (!Frontmatter.ValidAreas.Contains(area))
        {
            ConsoleOutput.WriteError($"Invalid area '{area}'. Must be one of: {string.Join(", ", Frontmatter.ValidAreas)}");
            return ExitCodes.ToolError;
        }

        if (!Enum.TryParse<IssueSeverity>(severity, ignoreCase: true, out var parsedSeverity))
        {
            var valid = string.Join(", ", Enum.GetNames<IssueSeverity>().Select(n => n.ToLowerInvariant()));
            ConsoleOutput.WriteError($"Invalid severity '{severity}'. Must be one of: {valid}");
            return ExitCodes.ToolError;
        }

        var foundByInput = foundBy ?? "manual";
        if (!Enum.TryParse<IssueFoundBy>(foundByInput, ignoreCase: true, out var parsedFoundBy))
        {
            var valid = string.Join(", ", Enum.GetNames<IssueFoundBy>().Select(n => n.ToLowerInvariant()));
            ConsoleOutput.WriteError($"Invalid found-by '{foundBy}'. Must be one of: {valid}");
            return ExitCodes.ToolError;
        }

        if (body != null && bodyFile != null)
        {
            ConsoleOutput.WriteError("Cannot specify both --body and --body-file. Use one or the other.");
            return ExitCodes.ToolError;
        }

        string? bodyContent = body;
        if (bodyFile != null)
        {
            if (!File.Exists(bodyFile))
            {
                ConsoleOutput.WriteError($"Body file not found: {bodyFile}");
                return ExitCodes.ToolError;
            }
            bodyContent = File.ReadAllText(bodyFile);
        }

        if (bodyContent != null)
        {
            bodyContent = bodyContent.Trim();
            if (bodyContent.Length == 0) bodyContent = null;
        }

        var issuesPath = IssueCommand.GetIssuesPath();
        Directory.CreateDirectory(issuesPath);

        var lockPath = Path.Combine(issuesPath, ".lock");
        FileStream? lockFile = null;
        try
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    lockFile = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    break;
                }
                catch (IOException) when (attempt < 4)
                {
                    Thread.Sleep(200);
                }
            }

            if (lockFile == null)
            {
                ConsoleOutput.WriteError("Could not acquire issue lock. Another process may be creating an issue. Try again.");
                return ExitCodes.ToolError;
            }

            var newId = ScanMaxId(issuesPath) + 1;
            var slug = Slugify(title);
            var fileName = $"{newId:D4}-{slug}.md";
            var filePath = Path.Combine(issuesPath, fileName);

            var bodySection = BuildBodySection(bodyContent);
            var content = $"""
                ---
                id: {newId}
                area: {area}
                type: issue
                severity: {parsedSeverity.ToString().ToLowerInvariant()}
                status: {IssueStatus.Open.ToString().ToLowerInvariant()}
                found-by: {parsedFoundBy.ToString().ToLowerInvariant()}
                date: {DateTime.UtcNow:yyyy-MM-dd}
                ---

                # {title}

                {bodySection}
                """;

            File.WriteAllText(filePath, content);
            Console.WriteLine($"Created issue #{newId}: {fileName}");
        }
        finally
        {
            lockFile?.Dispose();
        }

        return ExitCodes.Success;
    }

    internal static string Slugify(string title)
    {
        var slug = title.ToLowerInvariant();
        slug = SlugNonAlnumRegex().Replace(slug, "-");
        slug = MultipleHyphensRegex().Replace(slug, "-");
        slug = slug.Trim('-');
        if (slug.Length > 80)
            slug = slug[..80].TrimEnd('-');
        return slug;
    }

    internal static int ScanMaxId(string issuesPath)
    {
        var maxId = 0;
        var dirs = new[] { issuesPath, Path.Combine(issuesPath, "resolved") };

        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.GetFiles(dir, "*.md"))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var match = IdPrefixRegex().Match(fileName);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var id))
                {
                    if (id > maxId) maxId = id;
                }
            }
        }

        return maxId;
    }

    private const string ReproductionResolutionPlaceholder = """
        ## Reproduction

        (Steps to reproduce, if applicable)

        ## Resolution

        (Filled when resolved)
        """;

    internal static string BuildBodySection(string? bodyContent)
    {
        if (bodyContent == null)
        {
            return $"""
                ## Description

                (Describe the issue)

                {ReproductionResolutionPlaceholder}
                """;
        }

        if (StructuralHeadingRegex().IsMatch(bodyContent))
        {
            return $"""
                ## Description

                {bodyContent}
                """;
        }

        return $"""
            ## Description

            {bodyContent}

            {ReproductionResolutionPlaceholder}
            """;
    }

    [GeneratedRegex(@"^(\d+)-")]
    private static partial Regex IdPrefixRegex();

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex SlugNonAlnumRegex();

    [GeneratedRegex(@"-+")]
    private static partial Regex MultipleHyphensRegex();

    [GeneratedRegex(@"^## (Reproduction|Resolution)\b", RegexOptions.Multiline)]
    private static partial Regex StructuralHeadingRegex();
}

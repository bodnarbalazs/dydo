namespace DynaDocs.Commands;

using System.Text.RegularExpressions;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

internal static partial class IssueCreateHandler
{
    internal const string SummaryPlaceholder = "(One-line summary)";

    public static int Execute(string title, string area, string severity, string? foundBy, string? summary = null, string? body = null, string? bodyFile = null)
    {
        if (!TryValidateMetadata(area, severity, foundBy, out var meta))
            return ExitCodes.ToolError;

        if (!TryResolveBody(body, bodyFile, out var bodyContent))
            return ExitCodes.ToolError;

        var summaryLine = NormalizeSummary(summary);

        var issuesPath = IssueCommand.GetIssuesPath();
        Directory.CreateDirectory(issuesPath);

        using var lockFile = AcquireIssueLock(Path.Combine(issuesPath, ".lock"));
        if (lockFile == null)
        {
            ConsoleOutput.WriteError("Could not acquire issue lock. Another process may be creating an issue. Try again.");
            return ExitCodes.ToolError;
        }

        var newId = ScanMaxId(issuesPath) + 1;
        var fileName = $"{newId:D4}-{Slugify(title)}.md";
        var filePath = Path.Combine(issuesPath, fileName);

        File.WriteAllText(filePath, RenderIssueContent(newId, title, meta, summaryLine, bodyContent));
        Console.WriteLine($"Created issue #{newId}: {fileName}");

        return ExitCodes.Success;
    }

    private record IssueMeta(string Area, IssueSeverity Severity, IssueFoundBy FoundBy);

    private static bool TryValidateMetadata(string area, string severity, string? foundBy, out IssueMeta meta)
    {
        meta = default!;

        if (!Frontmatter.ValidAreas.Contains(area))
        {
            ConsoleOutput.WriteError($"Invalid area '{area}'. Must be one of: {string.Join(", ", Frontmatter.ValidAreas)}");
            return false;
        }

        if (!Enum.TryParse<IssueSeverity>(severity, ignoreCase: true, out var parsedSeverity))
        {
            ConsoleOutput.WriteError($"Invalid severity '{severity}'. Must be one of: {EnumNames<IssueSeverity>()}");
            return false;
        }

        var foundByInput = foundBy ?? "manual";
        if (!Enum.TryParse<IssueFoundBy>(foundByInput, ignoreCase: true, out var parsedFoundBy))
        {
            ConsoleOutput.WriteError($"Invalid found-by '{foundBy}'. Must be one of: {EnumNames<IssueFoundBy>()}");
            return false;
        }

        meta = new IssueMeta(area, parsedSeverity, parsedFoundBy);
        return true;
    }

    private static string EnumNames<T>() where T : struct, Enum =>
        string.Join(", ", Enum.GetNames<T>().Select(n => n.ToLowerInvariant()));

    private static bool TryResolveBody(string? body, string? bodyFile, out string? bodyContent)
    {
        bodyContent = null;

        if (body != null && bodyFile != null)
        {
            ConsoleOutput.WriteError("Cannot specify both --body and --body-file. Use one or the other.");
            return false;
        }

        if (bodyFile != null)
        {
            if (!File.Exists(bodyFile))
            {
                ConsoleOutput.WriteError($"Body file not found: {bodyFile}");
                return false;
            }
            bodyContent = File.ReadAllText(bodyFile);
        }
        else
        {
            bodyContent = body;
        }

        bodyContent = bodyContent?.Trim();
        if (bodyContent?.Length == 0) bodyContent = null;
        return true;
    }

    private static string NormalizeSummary(string? summary)
    {
        var trimmed = summary?.Trim();
        return string.IsNullOrEmpty(trimmed) ? SummaryPlaceholder : trimmed;
    }

    private static FileStream? AcquireIssueLock(string lockPath)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(200);
            }
        }
        return null;
    }

    // Agent/vendor/model provenance stamping was carved out with the claim ceremony (DR-041):
    // there is no runtime agent identity, so issues are created without found-by-agent provenance.
    private static string RenderIssueContent(int id, string title, IssueMeta meta, string summaryLine, string? bodyContent)
    {
        return $"""
            ---
            title: {title}
            id: {id}
            area: {meta.Area}
            type: issue
            severity: {meta.Severity.ToString().ToLowerInvariant()}
            status: {IssueStatus.Open.ToString().ToLowerInvariant()}
            found-by: {meta.FoundBy.ToString().ToLowerInvariant()}
            date: {DateTime.UtcNow:yyyy-MM-dd}
            ---

            # {title}

            {summaryLine}

            {BuildBodySection(bodyContent)}
            """;
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

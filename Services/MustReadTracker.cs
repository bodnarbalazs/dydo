namespace DynaDocs.Services;

using DynaDocs.Models;
using DynaDocs.Utils;

public class MustReadTracker
{
    private readonly string _basePath;
    private readonly IConfigService _configService;
    private readonly IAuditService _auditService;
    private readonly Func<string, string> _getAgentWorkspace;

    public MustReadTracker(
        string basePath,
        IConfigService configService,
        IAuditService auditService,
        Func<string, string> getAgentWorkspace)
    {
        _basePath = basePath;
        _configService = configService;
        _auditService = auditService;
        _getAgentWorkspace = getAgentWorkspace;
    }

    /// <summary>
    /// Computes the list of must-read files for a given role by inspecting the mode file's links.
    /// Filters out files already read in the current audit session.
    /// </summary>
    public List<string> ComputeUnreadMustReads(string agentName, string role, string? sessionId)
    {
        var workspace = _getAgentWorkspace(agentName);
        var modeFilePath = Path.Combine(workspace, "modes", $"{role}.md");

        if (!File.Exists(modeFilePath))
            return [];

        var content = File.ReadAllText(modeFilePath);
        var parser = new MarkdownParser();
        var links = parser.ExtractLinks(content);

        var projectRoot = _configService.GetProjectRoot(_basePath) ?? _basePath;
        var mustReads = new List<string>();

        foreach (var link in links)
        {
            if (link.Type == LinkType.External) continue;
            if (string.IsNullOrEmpty(link.Target)) continue;

            var resolved = PathUtils.ResolvePath(modeFilePath, link.Target);

            if (!File.Exists(resolved)) continue;

            var targetContent = File.ReadAllText(resolved);
            var frontmatter = parser.ExtractFrontmatter(targetContent);

            if (frontmatter?.MustRead == true)
            {
                var relativePath = PathUtils.NormalizePath(Path.GetRelativePath(projectRoot, resolved));
                mustReads.Add(relativePath);
            }
        }

        // Add the mode file itself (always implicitly must-read)
        var modeRelative = PathUtils.NormalizePath(Path.GetRelativePath(projectRoot, modeFilePath));
        mustReads.Add(modeRelative);

        mustReads = mustReads.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // Filter out files already read in this session
        if (!string.IsNullOrEmpty(sessionId))
        {
            try
            {
                var session = _auditService.GetSession(sessionId);
                if (session != null)
                {
                    var readPaths = session.Events
                        .Where(e => e.EventType == AuditEventType.Read && !string.IsNullOrEmpty(e.Path))
                        .Select(e => NormalizeMustReadPath(e.Path!))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    mustReads.RemoveAll(p => readPaths.Contains(NormalizeMustReadPath(p)));
                }
            }
            catch
            {
                // Audit service failure should not block role setting
            }
        }

        return mustReads;
    }

    public static string NormalizeMustReadPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        var dydoIndex = normalized.IndexOf("dydo/", StringComparison.OrdinalIgnoreCase);
        return dydoIndex >= 0 ? normalized[dydoIndex..] : normalized;
    }
}

namespace DynaDocs.Services;

using DynaDocs.Models;
using DynaDocs.Utils;

public class DocScanner : IDocScanner
{
    private readonly IMarkdownParser _parser;
    private readonly IConfigService _configService;

    public DocScanner(IMarkdownParser parser, IConfigService? configService = null)
    {
        _parser = parser;
        _configService = configService ?? new ConfigService();
    }

    public List<DocFile> ScanDirectory(string path)
    {
        var docs = new List<DocFile>();
        var mdFiles = Directory.GetFiles(path, "*.md", SearchOption.AllDirectories);
        var excludes = GetScanExcludes(path);

        foreach (var file in mdFiles)
        {
            var relative = PathUtils.NormalizePath(Path.GetRelativePath(path, file));
            if (IsExcluded(relative, excludes))
                continue;

            docs.Add(_parser.Parse(file, path));
        }

        return docs;
    }

    public List<string> GetAllFolders(string path)
    {
        var folders = new List<string> { path };
        folders.AddRange(Directory.GetDirectories(path, "*", SearchOption.AllDirectories));
        return folders;
    }

    private List<string> GetScanExcludes(string basePath)
    {
        var merged = new List<string>(ConfigFactory.DydoInternalScanExclude);

        var config = _configService.LoadConfig(basePath);
        if (config == null)
            return merged;

        foreach (var entry in config.ScanExclude)
        {
            if (string.IsNullOrWhiteSpace(entry))
                continue;
            if (merged.Any(m => m.Equals(entry, StringComparison.OrdinalIgnoreCase)))
                continue;
            merged.Add(entry);
        }

        return merged;
    }

    private static bool IsExcluded(string normalizedRelativePath, List<string> excludes)
    {
        foreach (var prefix in excludes)
        {
            if (normalizedRelativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

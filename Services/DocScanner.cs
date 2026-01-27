namespace DynaDocs.Services;

using DynaDocs.Models;

public class DocScanner : IDocScanner
{
    private readonly IMarkdownParser _parser;

    public DocScanner(IMarkdownParser parser)
    {
        _parser = parser;
    }

    public List<DocFile> ScanDirectory(string path)
    {
        var docs = new List<DocFile>();
        var mdFiles = Directory.GetFiles(path, "*.md", SearchOption.AllDirectories);

        foreach (var file in mdFiles)
        {
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
}

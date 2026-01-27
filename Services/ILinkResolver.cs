namespace DynaDocs.Services;

using DynaDocs.Models;

public interface ILinkResolver
{
    bool ResolveLink(DocFile sourceDoc, LinkInfo link, List<DocFile> allDocs, string basePath);
    string? FindFileByName(string fileName, List<DocFile> allDocs);
    bool ValidateAnchor(string? anchor, DocFile targetDoc);
}

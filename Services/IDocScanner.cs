namespace DynaDocs.Services;

using DynaDocs.Models;

public interface IDocScanner
{
    List<DocFile> ScanDirectory(string path);
    List<string> GetAllFolders(string path);
}

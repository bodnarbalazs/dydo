namespace DynaDocs.Services;

using DynaDocs.Models;

public interface IIndexGenerator
{
    string Generate(List<DocFile> docs, string basePath);
}

namespace DynaDocs.Services;

using DynaDocs.Models;

public interface IDocGraph
{
    void Build(List<DocFile> docs, string basePath);
    List<(string Doc, int LineNumber)> GetIncoming(string docPath);
    List<(string Doc, int Degree)> GetWithinDegree(string docPath, int maxDegree);
    bool HasDoc(string docPath);
}

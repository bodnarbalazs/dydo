namespace DynaDocs.Services;

using DynaDocs.Models;

public interface IRoleDefinitionService
{
    Dictionary<string, List<string>> ResolvePathSets(DydoConfig? config);
}

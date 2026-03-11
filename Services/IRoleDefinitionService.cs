namespace DynaDocs.Services;

using DynaDocs.Models;

public interface IRoleDefinitionService
{
    List<RoleDefinition> LoadRoleDefinitions(string basePath);

    Dictionary<string, (List<string> Writable, List<string> ReadOnly)> BuildPermissionMap(
        List<RoleDefinition> roles, Dictionary<string, List<string>> pathSets);

    Dictionary<string, List<string>> ResolvePathSets(DydoConfig? config);

    bool ValidateRoleDefinition(RoleDefinition role, out List<string> errors);

    void WriteBaseRoleDefinitions(string basePath);
}

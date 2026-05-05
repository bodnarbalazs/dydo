namespace DynaDocs.Services;

using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Serialization;

/// <summary>
/// Loads <c>dydo/_system/types.json</c> and merges it with the built-in
/// <see cref="Frontmatter.ValidTypes"/> baseline. The file is read once on
/// first access and cached for the instance's lifetime — callers construct
/// one instance per check pass.
/// </summary>
public class FrontmatterTypesService : IFrontmatterTypesService
{
    public const string TypesJsonRelativePath = "_system/types.json";

    private readonly string _dydoRoot;
    private IReadOnlyList<string>? _cachedTypes;

    public FrontmatterTypesService(string dydoRoot)
    {
        _dydoRoot = dydoRoot;
    }

    public IReadOnlyList<string> GetValidTypes()
    {
        if (_cachedTypes != null) return _cachedTypes;

        _cachedTypes = LoadAndMerge(_dydoRoot);
        return _cachedTypes;
    }

    private static IReadOnlyList<string> LoadAndMerge(string dydoRoot)
    {
        var path = Path.Combine(dydoRoot, TypesJsonRelativePath);
        if (!File.Exists(path))
            return Frontmatter.ValidTypes;

        string[]? userTypes = null;
        try
        {
            var json = File.ReadAllText(path);
            userTypes = JsonSerializer.Deserialize(json, TypesJsonContext.Default.StringArray);
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"  Warning: {TypesJsonRelativePath} is malformed ({ex.Message}); falling back to baseline types.");
        }

        if (userTypes == null || userTypes.Length == 0)
            return Frontmatter.ValidTypes;

        var merged = new List<string>(Frontmatter.ValidTypes);
        var seen = new HashSet<string>(merged, StringComparer.Ordinal);
        foreach (var t in userTypes)
        {
            if (string.IsNullOrWhiteSpace(t)) continue;
            if (seen.Add(t)) merged.Add(t);
        }
        return merged;
    }
}

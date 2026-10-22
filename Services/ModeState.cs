namespace DynaDocs.Services;

using System.Text.Json;
using DynaDocs.Serialization;

/// <summary>
/// The machine mode behind <c>dydo mode</c>: one flag-to-value map per user and machine, kept beside
/// the saved-map cache under local application data (DR 052) so every project and every agent on
/// this machine reads the same value. A missing or unreadable file reads as the defaults, and the
/// next set rewrites it.
/// </summary>
internal sealed class ModeState(string? root = null)
{
    /// <summary>Every flag with its allowed values; the first value is the default.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Flags = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["overnight"] = ["off", "on"],
    };

    private readonly string _path = Path.Combine(
        root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dydo"),
        "mode.json");

    /// <summary>Every flag with its current value, defaults filling whatever the file lacks.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Read()
    {
        var stored = Load();
        return Flags.Select(flag => KeyValuePair.Create(flag.Key,
            stored.TryGetValue(flag.Key, out var value) && flag.Value.Contains(value) ? value : flag.Value[0])).ToList();
    }

    /// <summary>Stores a value the caller has checked against <see cref="Flags"/>.</summary>
    public void Set(string flag, string value)
    {
        var values = Read().ToDictionary(StringComparer.Ordinal);
        values[flag] = value;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(values, DydoDefaultJsonContext.Default.DictionaryStringString));
    }

    private Dictionary<string, string> Load()
    {
        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(_path), DydoDefaultJsonContext.Default.DictionaryStringString) ?? [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }
}

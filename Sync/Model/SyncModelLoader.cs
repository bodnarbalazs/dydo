namespace DynaDocs.Sync.Model;

using System.Text.Json;
using DynaDocs.Serialization;
using DynaDocs.Services;

/// <summary>
/// Loads the sync model from <c>dydo/_system/sync-model.json</c> (slice brief §1/§2). When the file is
/// missing it is auto-seeded from the built-in default shipped as a template: the dydo binary writes it
/// at runtime, so the file lands under the agent-guarded <c>_system</c> tree without an agent ever
/// editing it. Thereafter the on-disk file is the single source of truth a project edits to define its
/// own object types.
/// </summary>
public static class SyncModelLoader
{
    public const string ModelFileName = "sync-model.json";
    public const string DefaultTemplateName = "sync-model.template.json";

    public static string PathFor(string dydoRoot) =>
        Path.Combine(dydoRoot, "_system", ModelFileName);

    public static SyncModel Load(string dydoRoot)
    {
        var path = PathFor(dydoRoot);
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, TemplateGenerator.ReadBuiltInTemplate(DefaultTemplateName));
        }

        var model = JsonSerializer.Deserialize(File.ReadAllText(path), SyncModelJsonContext.Default.SyncModel)
            ?? throw new SyncModelException($"sync model at {path} is empty or invalid");
        if (model.Objects.Count == 0)
            throw new SyncModelException($"sync model at {path} defines no object types");
        return model;
    }
}

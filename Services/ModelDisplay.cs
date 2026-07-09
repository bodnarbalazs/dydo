namespace DynaDocs.Services;

using DynaDocs.Models;

/// <summary>
/// One shared resolver (c1-6) for turning a raw runtime model id into the human display name
/// balazs wants on provenance surfaces (<c>Opus 4.8</c>, not <c>claude</c>). Every surface —
/// issues, messages, reviews, task records (all via <see cref="ArtifactProvenance"/>), plus
/// <c>dydo whoami</c> and <c>dydo agent list</c> — routes through here so the rendering rule
/// cannot drift per-surface.
///
/// Rules:
/// <list type="bullet">
///   <item>A known id maps to its display name.</item>
///   <item>An unknown id passes through verbatim (never guessed, never a vendor name).</item>
///   <item>The vendor is used ONLY as a fallback when the model itself is unknown
///     (<see cref="ResolveOrVendor"/>).</item>
/// </list>
///
/// The effective map is the configured <c>models.display-names</c> when it is non-empty, else
/// the shipped defaults (<see cref="ConfigFactory.DefaultDisplayNames"/>) — the same
/// absent-section-defaults contract <see cref="CodexDispatchConfig"/> uses, so the defaults
/// take effect even for a dydo.json that predates the key.
/// </summary>
internal static class ModelDisplay
{
    /// <summary>
    /// The display-name map that actually applies: a configured non-empty map is authoritative;
    /// an absent or empty map resolves to the shipped defaults.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> EffectiveDisplayNames(ModelsConfig? models)
    {
        var configured = models?.DisplayNames;
        if (configured is { Count: > 0 })
            return configured;
        return ConfigFactory.DefaultDisplayNames;
    }

    /// <summary>
    /// Known id → display name; unknown ids pass through verbatim. An empty/unknown model id is
    /// returned as the canonical unknown sentinel (callers use <see cref="ResolveOrVendor"/> to
    /// apply the vendor fallback where a single identity string is shown).
    /// </summary>
    public static string Resolve(string? modelId, ModelsConfig? models)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            return AgentSession.UnknownModel;

        var id = modelId.Trim();
        var effective = EffectiveDisplayNames(models);
        return effective.TryGetValue(id, out var name) ? name : id;
    }

    /// <summary>
    /// Provenance/identity rendering: the display model when it is known, otherwise the vendor
    /// as the only fallback (the rendering rule shared across every provenance surface).
    /// </summary>
    public static string ResolveOrVendor(string? modelId, string? vendor, ModelsConfig? models)
    {
        var normalizedModel = AgentSession.NormalizeModel(modelId);
        if (normalizedModel == AgentSession.UnknownModel)
            return string.IsNullOrWhiteSpace(vendor) ? AgentSession.UnknownHost : vendor;

        return Resolve(normalizedModel, models);
    }
}

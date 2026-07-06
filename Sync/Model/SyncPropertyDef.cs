namespace DynaDocs.Sync.Model;

using System.Text.Json.Serialization;

/// <summary>
/// One property of a sync-model object type (slice brief §1): its <see cref="Type"/>
/// (title|select|number|date|rich_text|checkbox|relation|formula|rollup), the <see cref="Options"/> a
/// select offers, and for a relation the <see cref="To"/> object type it points at. View-agnostic — how
/// the property is realised in Notion (or any other view) lives in that view's adapter, never here.
/// <para>
/// DR 029/030 additions: <see cref="Colors"/> tags each select option with a Notion palette color;
/// <see cref="Expression"/> is a formula body; and <see cref="RollupRelation"/> / <see cref="RollupProperty"/>
/// / <see cref="RollupFunction"/> configure a rollup over a reverse relation. <see cref="Reverse"/> names the
/// synced back-reference a dual-property relation creates on its target, so a rollup can reference it by name.
/// A computed property (formula/rollup) is recognised by its <see cref="Type"/> alone — the mapper skips those
/// types on read and write — so no flag is needed to mark it.
/// </para>
/// </summary>
public sealed class SyncPropertyDef
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("options")]
    public List<string>? Options { get; set; }

    /// <summary>Select option value → Notion palette color (default/gray/brown/orange/yellow/green/blue/
    /// purple/pink/red). Optional and backward-compatible: an option absent from the map provisions with
    /// Notion's default color, so a plain <see cref="Options"/>-only model still loads unchanged.</summary>
    [JsonPropertyName("colors")]
    public Dictionary<string, string>? Colors { get; set; }

    [JsonPropertyName("to")]
    public string? To { get; set; }

    /// <summary>For a relation, the name of the synced back-reference property a dual-property relation
    /// creates on its target (DR 029 §5). When set the relation is provisioned dual-property; when absent
    /// it is single-property. The reverse is view-only and never appears in the model as its own property.</summary>
    [JsonPropertyName("reverse")]
    public string? Reverse { get; set; }

    /// <summary>An engine-owned property the sync engine writes ONE-WAY to Notion from state it derives
    /// itself, never from frontmatter and never read back (DR 030 §3 — <c>last-activity</c>). Unlike a
    /// formula/rollup (which Notion computes), an engine-computed property has a
    /// real Notion type (e.g. <c>date</c>) that the engine populates: the spine writes the value on every
    /// push, and the adapter drops it on read so it can never enter a base snapshot or frontmatter — which
    /// would otherwise turn every sync into an edit loop. Defaults false, backward-compatible with plain
    /// models.</summary>
    [JsonPropertyName("engineComputed")]
    public bool EngineComputed { get; set; }

    /// <summary>A formula property's Notion expression (view-only).</summary>
    [JsonPropertyName("expression")]
    public string? Expression { get; set; }

    /// <summary>A compute-only helper property (an intermediate rollup/formula projection, or an engine-owned
    /// column) the provisioned views hide by default — it exists to feed other computed columns, not for the
    /// human to read. Defaults false; a plain human-facing property shows in every view.</summary>
    [JsonPropertyName("hidden")]
    public bool Hidden { get; set; }

    /// <summary>A rollup property's source relation property name — the reverse relation on this type a
    /// child's dual-property relation created (view-only).</summary>
    [JsonPropertyName("rollupRelation")]
    public string? RollupRelation { get; set; }

    /// <summary>The property on the related rows a rollup aggregates.</summary>
    [JsonPropertyName("rollupProperty")]
    public string? RollupProperty { get; set; }

    /// <summary>The rollup aggregation function (e.g. <c>percent_checked</c>).</summary>
    [JsonPropertyName("rollupFunction")]
    public string? RollupFunction { get; set; }

    /// <summary>
    /// For a select property, the option value → repo subfolder routing (slice brief §3): a doc whose
    /// value for this property matches a key is filed under that subfolder, and an unmapped value goes to
    /// the dir root. Folder placement is derived presentation — status stays canonical in frontmatter — so
    /// the engine pools docs from every subfolder and re-files a doc when this value changes, never losing
    /// it. Absent for properties that don't route.
    /// </summary>
    [JsonPropertyName("folders")]
    public Dictionary<string, string>? Folders { get; set; }
}

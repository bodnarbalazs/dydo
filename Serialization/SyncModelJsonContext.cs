namespace DynaDocs.Serialization;

using System.Text.Json.Serialization;
using DynaDocs.Sync.Model;

/// <summary>
/// Source-generated JSON for the sync-model file (slice brief §1) — Native AOT, no reflection
/// serialization. Read on every sync to drive provisioning and mapping; written once when auto-seeding
/// the default model, where the template text is copied verbatim.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SyncModel))]
internal partial class SyncModelJsonContext : JsonSerializerContext { }

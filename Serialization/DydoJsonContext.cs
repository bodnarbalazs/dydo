namespace DynaDocs.Serialization;

using System.Text.Json;
using System.Text.Json.Serialization;
using DynaDocs.Models;

/// <summary>
/// JSON serializer context for dydo.json configuration files.
/// Uses camelCase naming policy to match the config file format.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DydoConfig))]
[JsonSerializable(typeof(StructureConfig))]
[JsonSerializable(typeof(ModelsConfig))]
[JsonSerializable(typeof(Dictionary<string, Dictionary<string, string>>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, bool>))]
[JsonSerializable(typeof(NudgeConfig))]
[JsonSerializable(typeof(List<NudgeConfig>))]
[JsonSerializable(typeof(List<string>))]
internal partial class DydoConfigJsonContext : JsonSerializerContext { }

/// <summary>
/// JSON serializer context for types that use default naming (PascalCase)
/// or explicit JsonPropertyName attributes.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(HookInput))]
[JsonSerializable(typeof(ToolInputData))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class DydoDefaultJsonContext : JsonSerializerContext { }

/// <summary>
/// Lenient JSON context for hand-edited dydo files that may contain
/// comments or trailing commas (e.g. _system/types.json).
/// </summary>
[JsonSourceGenerationOptions(
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(string[]))]
internal partial class TypesJsonContext : JsonSerializerContext { }

/// <summary>

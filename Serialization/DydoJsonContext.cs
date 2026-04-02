namespace DynaDocs.Serialization;

using System.Text.Json.Serialization;
using DynaDocs.Commands;
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
[JsonSerializable(typeof(PathsConfig))]
[JsonSerializable(typeof(AgentsConfig))]
[JsonSerializable(typeof(DispatchConfig))]
[JsonSerializable(typeof(TasksConfig))]
[JsonSerializable(typeof(RoleDefinition))]
[JsonSerializable(typeof(RoleConstraint))]
[JsonSerializable(typeof(List<RoleConstraint>))]
[JsonSerializable(typeof(ConditionalMustRead))]
[JsonSerializable(typeof(ConditionalMustReadCondition))]
[JsonSerializable(typeof(List<ConditionalMustRead>))]
[JsonSerializable(typeof(Dictionary<string, bool>))]
[JsonSerializable(typeof(Dictionary<string, List<string>>))]
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
[JsonSerializable(typeof(AgentSession))]
[JsonSerializable(typeof(AuditEvent))]
[JsonSerializable(typeof(AuditSession))]
[JsonSerializable(typeof(ProjectSnapshot))]
[JsonSerializable(typeof(List<AuditEvent>))]
[JsonSerializable(typeof(List<AuditSession>))]
[JsonSerializable(typeof(MergedEvent))]
[JsonSerializable(typeof(List<MergedEvent>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(SnapshotBaseline))]
[JsonSerializable(typeof(SnapshotRef))]
[JsonSerializable(typeof(SnapshotDelta))]
[JsonSerializable(typeof(WaitMarker))]
[JsonSerializable(typeof(ReplyPendingMarker))]
[JsonSerializable(typeof(DispatchMarker))]
[JsonSerializable(typeof(GuardLiftMarker))]
[JsonSerializable(typeof(QueueEntry))]
[JsonSerializable(typeof(QueueActiveEntry))]
internal partial class DydoDefaultJsonContext : JsonSerializerContext { }

namespace DynaDocs.Services.Map.Contract;

internal sealed record MapSnapshotEnvelope(int Version, string ProjectId, MapSnapshot Snapshot);

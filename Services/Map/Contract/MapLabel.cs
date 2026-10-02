namespace DynaDocs.Services.Map.Contract;

/// <summary>An issue label as Linear names and colours it; <see cref="Color"/> is Linear's hex.</summary>
internal sealed record MapLabel(string Name, string Color);

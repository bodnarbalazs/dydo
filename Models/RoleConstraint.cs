namespace DynaDocs.Models;

public class RoleConstraint
{
    public required string Type { get; init; }
    public string? FromRole { get; init; }
    public string? TargetRole { get; init; }
    public List<string>? RequiredRoles { get; init; }
    public int? MaxCount { get; init; }
    public bool OnlyWhenDispatched { get; init; }
    public required string Message { get; init; }
}

namespace DynaDocs.Sync.Projection;

public sealed record ProjectedBodyResult(string? Body, ProjectedBodyConflict? Conflict)
{
    public bool IsSuccess => Conflict is null;

    public static ProjectedBodyResult Success(string body) => new(body, null);

    public static ProjectedBodyResult Failed(string reason) => new(null, new ProjectedBodyConflict(reason));
}

namespace DynaDocs.Services;

/// <summary>The rename probe moved a worktree aside and could not move it back; <see cref="Probe"/> is where it is.</summary>
internal sealed class ProbeStrandedException(string probe, string reason)
    : IOException($"the busy check renamed it to {probe} and could not move it back ({reason})")
{
    public string Probe => probe;
}

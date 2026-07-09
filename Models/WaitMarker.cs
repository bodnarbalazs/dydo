namespace DynaDocs.Models;

using System.Text.Json.Serialization;

public class WaitMarker
{
    [JsonPropertyName("target")]
    public required string Target { get; init; }

    [JsonPropertyName("task")]
    public required string Task { get; init; }

    [JsonPropertyName("since")]
    public required DateTime Since { get; init; }

    [JsonPropertyName("listening")]
    public bool Listening { get; set; }

    [JsonPropertyName("pid")]
    public int? Pid { get; set; }

    // Durable general-wait marker (c1-2, #0254): a marker registered by `dydo wait --register`
    // for a host whose runtime cannot hold a foreground wait (e.g. a dispatched codex session).
    // Its <see cref="Pid"/> is the claimed session's host-liveness PID (not a live `dydo wait`
    // process), so the marker survives the host's tool timeouts and stays valid while the host
    // lives — the guard's general-wait check and the self-heal sweep treat it exactly like a
    // live wait because liveness is encoded in Pid. Dead host → stale → cleaned like a dead wait.
    [JsonPropertyName("durable")]
    public bool Durable { get; set; }
}

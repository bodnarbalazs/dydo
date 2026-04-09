namespace DynaDocs.Services;

using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Serialization;

public class GuardLiftService
{
    private readonly string _agentsDir;

    public GuardLiftService(string? basePath = null)
    {
        var root = basePath ?? Environment.CurrentDirectory;
        _agentsDir = Path.Combine(root, "dydo", "agents");
    }

    // Stored per-agent so it resolves through worktree junctions automatically
    private string MarkerPath(string agentName) =>
        Path.Combine(_agentsDir, agentName, ".guard-lift.json");

    public void Lift(string agentName, string humanName, int? minutes)
    {
        var marker = new GuardLiftMarker
        {
            Agent = agentName,
            LiftedBy = humanName,
            LiftedAt = DateTime.UtcNow,
            ExpiresAt = minutes.HasValue ? DateTime.UtcNow.AddMinutes(minutes.Value) : null
        };

        var json = JsonSerializer.Serialize(marker, DydoDefaultJsonContext.Default.GuardLiftMarker);
        var markerPath = MarkerPath(agentName);
        // Ensure directory exists — in worktrees the agents junction may be absent
        Directory.CreateDirectory(Path.GetDirectoryName(markerPath)!);
        File.WriteAllText(markerPath, json);
    }

    public void Restore(string agentName)
    {
        var path = MarkerPath(agentName);
        if (File.Exists(path))
            File.Delete(path);
    }

    public bool IsLifted(string agentName)
    {
        var path = MarkerPath(agentName);
        if (!File.Exists(path))
            return false;

        try
        {
            var json = File.ReadAllText(path);
            var marker = JsonSerializer.Deserialize(json, DydoDefaultJsonContext.Default.GuardLiftMarker);
            if (marker == null)
                return false;

            if (marker.ExpiresAt.HasValue && DateTime.UtcNow >= marker.ExpiresAt.Value)
            {
                // Expired — clean up
                File.Delete(path);
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public void ClearLift(string agentName) => Restore(agentName);
}

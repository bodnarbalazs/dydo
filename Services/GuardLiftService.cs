namespace DynaDocs.Services;

using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Serialization;

public class GuardLiftService
{
    private readonly string _liftsDir;

    public GuardLiftService(string? basePath = null)
    {
        var root = basePath ?? Environment.CurrentDirectory;
        _liftsDir = Path.Combine(root, "dydo", "_system", ".local", "guard-lifts");
    }

    private string MarkerPath(string agentName) =>
        Path.Combine(_liftsDir, $"{agentName}.json");

    public void Lift(string agentName, string humanName, int? minutes)
    {
        Directory.CreateDirectory(_liftsDir);

        var marker = new GuardLiftMarker
        {
            Agent = agentName,
            LiftedBy = humanName,
            LiftedAt = DateTime.UtcNow,
            ExpiresAt = minutes.HasValue ? DateTime.UtcNow.AddMinutes(minutes.Value) : null
        };

        var json = JsonSerializer.Serialize(marker, DydoDefaultJsonContext.Default.GuardLiftMarker);
        File.WriteAllText(MarkerPath(agentName), json);
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

    public void ClearLift(string agentName)
    {
        var path = MarkerPath(agentName);
        if (File.Exists(path))
            File.Delete(path);
    }
}

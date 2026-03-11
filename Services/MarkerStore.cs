namespace DynaDocs.Services;

using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Serialization;
using DynaDocs.Utils;

public class MarkerStore
{
    private readonly Func<string, string> _getAgentWorkspace;

    public MarkerStore(Func<string, string> getAgentWorkspace)
    {
        _getAgentWorkspace = getAgentWorkspace;
    }

    #region Wait Markers

    private string GetWaitingDir(string agentName) =>
        Path.Combine(_getAgentWorkspace(agentName), ".waiting");

    public void CreateWaitMarker(string agentName, string task, string targetAgent)
    {
        var dir = GetWaitingDir(agentName);
        Directory.CreateDirectory(dir);

        var marker = new WaitMarker
        {
            Target = targetAgent,
            Task = task,
            Since = DateTime.UtcNow
        };

        var sanitized = PathUtils.SanitizeForFilename(task);
        var path = Path.Combine(dir, $"{sanitized}.json");
        var json = JsonSerializer.Serialize(marker, DydoDefaultJsonContext.Default.WaitMarker);
        File.WriteAllText(path, json);
    }

    public List<WaitMarker> GetWaitMarkers(string agentName)
    {
        var dir = GetWaitingDir(agentName);
        if (!Directory.Exists(dir))
            return [];

        var markers = new List<WaitMarker>();
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var marker = JsonSerializer.Deserialize(json, DydoDefaultJsonContext.Default.WaitMarker);
                if (marker != null)
                    markers.Add(marker);
            }
            catch { }
        }

        return markers;
    }

    public bool RemoveWaitMarker(string agentName, string task)
    {
        var dir = GetWaitingDir(agentName);
        if (!Directory.Exists(dir))
            return false;

        var sanitized = PathUtils.SanitizeForFilename(task);
        var path = Path.Combine(dir, $"{sanitized}.json");
        if (!File.Exists(path))
            return false;

        File.Delete(path);
        return true;
    }

    public void ClearAllWaitMarkers(string agentName)
    {
        var dir = GetWaitingDir(agentName);
        if (Directory.Exists(dir))
            Directory.Delete(dir, true);
    }

    public bool UpdateWaitMarkerListening(string agentName, string task, int pid)
    {
        var dir = GetWaitingDir(agentName);
        var sanitized = PathUtils.SanitizeForFilename(task);
        var path = Path.Combine(dir, $"{sanitized}.json");

        if (!File.Exists(path))
            return false;

        try
        {
            var json = File.ReadAllText(path);
            var marker = JsonSerializer.Deserialize(json, DydoDefaultJsonContext.Default.WaitMarker);
            if (marker == null)
                return false;

            marker.Listening = true;
            marker.Pid = pid;

            var updated = JsonSerializer.Serialize(marker, DydoDefaultJsonContext.Default.WaitMarker);
            File.WriteAllText(path, updated);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void ResetWaitMarkerListening(string agentName, string task)
    {
        var dir = GetWaitingDir(agentName);
        var sanitized = PathUtils.SanitizeForFilename(task);
        var path = Path.Combine(dir, $"{sanitized}.json");

        if (!File.Exists(path))
            return;

        try
        {
            var json = File.ReadAllText(path);
            var marker = JsonSerializer.Deserialize(json, DydoDefaultJsonContext.Default.WaitMarker);
            if (marker == null) return;

            marker.Listening = false;
            marker.Pid = null;

            var updated = JsonSerializer.Serialize(marker, DydoDefaultJsonContext.Default.WaitMarker);
            File.WriteAllText(path, updated);
        }
        catch { }
    }

    public List<WaitMarker> GetNonListeningWaitMarkers(string agentName)
    {
        return GetWaitMarkers(agentName).Where(m => !m.Listening).ToList();
    }

    #endregion

    #region Reply-Pending Markers

    private string GetReplyPendingDir(string agentName) =>
        Path.Combine(_getAgentWorkspace(agentName), ".reply-pending");

    public void CreateReplyPendingMarker(string agentName, string task, string replyTo)
    {
        var dir = GetReplyPendingDir(agentName);
        Directory.CreateDirectory(dir);

        var marker = new ReplyPendingMarker
        {
            To = replyTo,
            Task = task,
            Since = DateTime.UtcNow
        };

        var sanitized = PathUtils.SanitizeForFilename(task);
        var path = Path.Combine(dir, $"{sanitized}.json");
        var json = JsonSerializer.Serialize(marker, DydoDefaultJsonContext.Default.ReplyPendingMarker);
        File.WriteAllText(path, json);
    }

    public List<ReplyPendingMarker> GetReplyPendingMarkers(string agentName)
    {
        var dir = GetReplyPendingDir(agentName);
        if (!Directory.Exists(dir))
            return [];

        var markers = new List<ReplyPendingMarker>();
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var marker = JsonSerializer.Deserialize(json, DydoDefaultJsonContext.Default.ReplyPendingMarker);
                if (marker != null)
                    markers.Add(marker);
            }
            catch { }
        }

        return markers;
    }

    public bool RemoveReplyPendingMarker(string agentName, string task)
    {
        var dir = GetReplyPendingDir(agentName);
        if (!Directory.Exists(dir))
            return false;

        var sanitized = PathUtils.SanitizeForFilename(task);
        var path = Path.Combine(dir, $"{sanitized}.json");
        if (!File.Exists(path))
            return false;

        File.Delete(path);
        return true;
    }

    public void ClearAllReplyPendingMarkers(string agentName)
    {
        var dir = GetReplyPendingDir(agentName);
        if (Directory.Exists(dir))
            Directory.Delete(dir, true);
    }

    #endregion
}

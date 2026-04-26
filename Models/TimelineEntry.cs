namespace DynaDocs.Models;

/// <summary>
/// A single event in the merged audit timeline (across multiple sessions),
/// shaped for serialization to the visualization HTML page.
/// </summary>
public class TimelineEntry
{
    public DateTime Timestamp { get; set; }
    public string Agent { get; set; } = "";
    public string EventType { get; set; } = "";
    public string? Path { get; set; }
    public string? Command { get; set; }
    public string? Role { get; set; }
    public string? Task { get; set; }
}

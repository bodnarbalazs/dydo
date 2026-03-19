namespace DynaDocs.Models;

public class InboxItem
{
    public required string Id { get; init; }
    public required string From { get; init; }
    public string? FromRole { get; init; }
    public required string Role { get; init; }
    public required string Task { get; init; }
    public DateTime Received { get; init; }
    public required string Brief { get; init; }
    public string? Origin { get; init; }
    public List<string> Files { get; init; } = [];
    public bool Escalated { get; init; }
    public DateTime? EscalatedAt { get; init; }
    public string? Type { get; init; }
    public string? Subject { get; init; }
    public string? Body { get; init; }
    public bool ReplyRequired { get; init; }
}

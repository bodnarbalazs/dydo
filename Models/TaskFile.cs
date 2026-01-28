namespace DynaDocs.Models;

public class TaskFile
{
    public required string Name { get; init; }
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    public string? ReviewSummary { get; set; }
    public List<string> FilesChanged { get; set; } = [];
    public DateTime Created { get; set; }
    public DateTime? Updated { get; set; }
    public string? AssignedAgent { get; set; }
}

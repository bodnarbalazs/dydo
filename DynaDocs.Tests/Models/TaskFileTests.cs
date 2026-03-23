namespace DynaDocs.Tests.Models;

using DynaDocs.Models;

public class TaskFileTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var task = new TaskFile { Name = "test-task" };

        Assert.Equal("test-task", task.Name);
        Assert.Equal(TaskStatus.Pending, task.Status);
        Assert.Null(task.ReviewSummary);
        Assert.Empty(task.FilesChanged);
        Assert.Equal(default, task.Created);
        Assert.Null(task.Updated);
        Assert.Null(task.AssignedAgent);
    }

    [Fact]
    public void Properties_CanBeSetAndRead()
    {
        var now = DateTime.UtcNow;
        var task = new TaskFile
        {
            Name = "fix-bug",
            Status = TaskStatus.Active,
            ReviewSummary = "Looks good",
            FilesChanged = ["Models/Foo.cs", "Services/Bar.cs"],
            Created = now,
            Updated = now.AddHours(1),
            AssignedAgent = "Dexter"
        };

        Assert.Equal("fix-bug", task.Name);
        Assert.Equal(TaskStatus.Active, task.Status);
        Assert.Equal("Looks good", task.ReviewSummary);
        Assert.Equal(2, task.FilesChanged.Count);
        Assert.Contains("Models/Foo.cs", task.FilesChanged);
        Assert.Equal(now, task.Created);
        Assert.Equal(now.AddHours(1), task.Updated);
        Assert.Equal("Dexter", task.AssignedAgent);
    }

    [Fact]
    public void Status_CanTransitionThroughAllStates()
    {
        var task = new TaskFile { Name = "lifecycle" };

        Assert.Equal(TaskStatus.Pending, task.Status);

        task.Status = TaskStatus.Active;
        Assert.Equal(TaskStatus.Active, task.Status);

        task.Status = TaskStatus.ReviewPending;
        Assert.Equal(TaskStatus.ReviewPending, task.Status);

        task.Status = TaskStatus.Closed;
        Assert.Equal(TaskStatus.Closed, task.Status);
    }

    [Fact]
    public void FilesChanged_DefaultsToEmptyList()
    {
        var task = new TaskFile { Name = "empty" };

        Assert.NotNull(task.FilesChanged);
        Assert.Empty(task.FilesChanged);

        task.FilesChanged.Add("test.cs");
        Assert.Single(task.FilesChanged);
    }
}

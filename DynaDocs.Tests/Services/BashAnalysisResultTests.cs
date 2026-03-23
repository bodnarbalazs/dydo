namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

public class BashAnalysisResultTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var result = new BashAnalysisResult();

        Assert.Empty(result.Operations);
        Assert.False(result.HasDangerousPattern);
        Assert.Null(result.DangerousPatternReason);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Operations_CanBeAdded()
    {
        var result = new BashAnalysisResult();
        result.Operations.Add(new FileOperation
        {
            Type = FileOperationType.Read,
            Path = "/test.txt",
            Command = "cat /test.txt"
        });

        Assert.Single(result.Operations);
        Assert.Equal(FileOperationType.Read, result.Operations[0].Type);
        Assert.Equal("/test.txt", result.Operations[0].Path);
    }

    [Fact]
    public void DangerousPattern_CanBeSet()
    {
        var result = new BashAnalysisResult
        {
            HasDangerousPattern = true,
            DangerousPatternReason = "rm -rf /"
        };

        Assert.True(result.HasDangerousPattern);
        Assert.Equal("rm -rf /", result.DangerousPatternReason);
    }

    [Fact]
    public void Warnings_CanBeAdded()
    {
        var result = new BashAnalysisResult();
        result.Warnings.Add("Uncertain path detected");
        result.Warnings.Add("Variable substitution");

        Assert.Equal(2, result.Warnings.Count);
        Assert.Contains("Uncertain path detected", result.Warnings);
    }
}

public class FileOperationTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var op = new FileOperation();

        Assert.Equal(default, op.Type);
        Assert.Equal(string.Empty, op.Path);
        Assert.Equal(string.Empty, op.Command);
        Assert.False(op.IsUncertain);
    }

    [Fact]
    public void Properties_CanBeSetAndRead()
    {
        var op = new FileOperation
        {
            Type = FileOperationType.Write,
            Path = "/output.txt",
            Command = "echo hello > /output.txt",
            IsUncertain = true
        };

        Assert.Equal(FileOperationType.Write, op.Type);
        Assert.Equal("/output.txt", op.Path);
        Assert.Equal("echo hello > /output.txt", op.Command);
        Assert.True(op.IsUncertain);
    }
}

public class FileOperationTypeTests
{
    [Theory]
    [InlineData(FileOperationType.Read, 0)]
    [InlineData(FileOperationType.Write, 1)]
    [InlineData(FileOperationType.Delete, 2)]
    [InlineData(FileOperationType.Execute, 3)]
    [InlineData(FileOperationType.PermissionChange, 4)]
    [InlineData(FileOperationType.Copy, 5)]
    [InlineData(FileOperationType.Move, 6)]
    public void EnumValues_HaveExpectedOrdinals(FileOperationType type, int expected)
    {
        Assert.Equal(expected, (int)type);
    }

    [Fact]
    public void AllValues_AreDefined()
    {
        var values = Enum.GetValues<FileOperationType>();
        Assert.Equal(7, values.Length);
    }
}

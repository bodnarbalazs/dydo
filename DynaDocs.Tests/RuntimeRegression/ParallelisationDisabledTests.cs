namespace DynaDocs.Tests.RuntimeRegression;

using System.Reflection;

/// <summary>
/// Pins the assembly-wide DisableTestParallelization invariant established by #0167.
/// Cross-test races on Console.Out/Error and other process-global statics are
/// structurally impossible only while this attribute is set; flipping it back to
/// parallel execution must be a deliberate, reviewed change.
/// </summary>
public class ParallelisationDisabledTests
{
    [Fact]
    public void Assembly_DisablesTestParallelization()
    {
        var attr = typeof(ParallelisationDisabledTests).Assembly
            .GetCustomAttribute<CollectionBehaviorAttribute>();

        Assert.NotNull(attr);
        Assert.True(attr!.DisableTestParallelization,
            "DynaDocs.Tests must run with DisableTestParallelization = true. "
            + "Static-mutation races (#0167) re-open if parallel execution is re-enabled.");
    }
}

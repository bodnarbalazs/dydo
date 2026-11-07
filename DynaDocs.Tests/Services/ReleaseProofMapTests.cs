namespace DynaDocs.Tests.Services;

using System.Reflection;
using System.Text.Json;
using DynaDocs.Tests.Workflow;

// DYD-840: every numerical proof that leaves instrumented coverage for the Release lane is mapped,
// and its coverage witness reads the same cases; the lane runs exactly the mapped proofs.
public class ReleaseProofMapTests
{
    private static readonly JsonElement Map = JsonDocument.Parse(
        File.ReadAllText(ReleaseWorkflowTests.RepositoryFile("DynaDocs.Tests", "coverage", "release-proofs.json"))).RootElement;

    private static readonly MethodInfo[] TestMethods = typeof(ReleaseProofMapTests).Assembly.GetTypes()
        .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        .Where(method => method.GetCustomAttributes<FactAttribute>().Any())
        .ToArray();

    [Fact]
    public void TheLaneFilters_SelectTheProofTrait()
    {
        var trait = BashCommandAnalyzerTests.PerformanceProof;

        Assert.Equal(trait, Map.GetProperty("trait").GetString());
        Assert.Equal($"{trait}={BashCommandAnalyzerTests.ReleaseLane}|{trait}={BashCommandAnalyzerTests.BothLanes}",
            Map.GetProperty("releaseFilter").GetString());
        Assert.Equal($"{trait}!={BashCommandAnalyzerTests.ReleaseLane}", Map.GetProperty("coverageFilter").GetString());
    }

    [Fact]
    public void EveryProofTraitedTest_IsMappedInItsLane()
    {
        var traited = TestMethods
            .Select(method => (Name: FullName(method), Lane: Lane(method)))
            .Where(test => test.Lane != null)
            .ToDictionary(test => test.Name, test => test.Lane);
        var mapped = Proofs().ToDictionary(proof => proof.GetProperty("proof").GetString()!,
            proof => proof.GetProperty("lane").GetString());
        mapped.Add(Map.GetProperty("laneCheck").GetString()!, BashCommandAnalyzerTests.ReleaseLane);

        Assert.Equal(mapped.OrderBy(pair => pair.Key), traited.OrderBy(pair => pair.Key));
    }

    [Fact]
    public void EveryReleaseProof_HasACoverageWitnessOverTheSameCases()
    {
        foreach (var proof in Proofs())
        {
            var proofMethod = Method(proof.GetProperty("proof").GetString()!);
            var witness = Method(proof.GetProperty("witness").GetString()!);
            var cases = proof.GetProperty("cases").GetInt32();
            var name = FullName(proofMethod);

            Assert.Equal(cases, CaseCount(proofMethod));
            Assert.True(proof.GetProperty("assertions").GetArrayLength() > 0, name);
            if (Lane(proofMethod) == BashCommandAnalyzerTests.BothLanes)
            {
                Assert.Same(proofMethod, witness);
                continue;
            }
            Assert.NotSame(proofMethod, witness);
            Assert.Null(Lane(witness));
            Assert.Equal(DataSource(proofMethod), DataSource(witness));
            Assert.Equal(cases, CaseCount(witness));
            Assert.Equal(proofMethod.GetParameters().Select(parameter => parameter.ParameterType),
                witness.GetParameters().Select(parameter => parameter.ParameterType));
        }
    }

    private static IEnumerable<JsonElement> Proofs() => Map.GetProperty("proofs").EnumerateArray();

    private static string FullName(MethodInfo method) => $"{method.DeclaringType!.FullName}.{method.Name}";

    private static MethodInfo Method(string name) => Assert.Single(TestMethods, method => FullName(method) == name);

    private static string? Lane(MethodInfo method) => method.GetCustomAttributesData()
        .Where(attribute => attribute.AttributeType == typeof(TraitAttribute)
            && (string?)attribute.ConstructorArguments[0].Value == BashCommandAnalyzerTests.PerformanceProof)
        .Select(attribute => (string?)attribute.ConstructorArguments[1].Value)
        .SingleOrDefault();

    // The one MemberData a mapped theory reads, or null for a fact.
    private static string? DataSource(MethodInfo method) =>
        method.GetCustomAttributes<MemberDataAttribute>().SingleOrDefault()?.MemberName;

    private static int CaseCount(MethodInfo method)
    {
        if (method.GetCustomAttribute<TheoryAttribute>() == null)
            return 1;
        var inline = method.GetCustomAttributes<InlineDataAttribute>().Count();
        var source = DataSource(method);
        if (source == null)
            return inline;
        var data = (IEnumerable<object[]>)method.DeclaringType!
            .GetProperty(source, BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        return inline + data.Count();
    }
}

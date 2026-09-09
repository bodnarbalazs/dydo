using System.Text.Json;
using GateMetrics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

try
{
    object result = args switch
    {
        ["--syntax"] => SyntaxFacts(Console.In.ReadToEnd()),
        ["--project", var project, "--root", var root] => await ProjectMetrics.CollectAsync(project, root),
        ["--assembly", var assembly, "--root", var root, "--project", var project] =>
            AssemblyMetrics.Collect(assembly, root, project),
        _ => throw new ArgumentException("Expected --syntax, --project <csproj> --root <root> or --assembly <dll> --root <root> --project <csproj>.")
    };
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    }));
    return 0;
}
catch (Exception error) when (error is ArgumentException or InvalidOperationException or IOException)
{
    Console.Error.WriteLine(error.Message);
    return 2;
}

static object SyntaxFacts(string source)
{
    var tree = CSharpSyntaxTree.ParseText(source);
    var errors = tree.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
    if (errors.Length != 0)
        throw new ArgumentException("C# syntax failure: " + string.Join("; ", errors.Select(item => item.ToString())));
    var methods = SourceMetrics.Measure(tree);
    var compilation = CSharpCompilation.Create("syntax-facts", [tree],
        [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
    return new { methods, tokens = SourceMetrics.Tokens(tree), namespaceEdges = NamespaceDependencies.Collect(compilation, [tree]) };
}

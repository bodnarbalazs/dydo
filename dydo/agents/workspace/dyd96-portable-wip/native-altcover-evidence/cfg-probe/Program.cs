using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using SonarAnalyzer.CSharp.Metrics;

const string source = """
class Subject
{
    int Straight(int x) { return x; }
    int If(int x) { if (x > 0) return 1; return 0; }
    int Ternary(int x) => x > 0 ? 1 : 0;
    bool Short(bool a, bool b) { if (a && b) return true; return false; }
    int Finally(int x) { try { return x; } finally { System.Console.WriteLine(x); } }
    int OneCatch(string x) { try { return int.Parse(x); } catch { return 0; } }
    int TwoCatch(string x) { try { return int.Parse(x); } catch (System.FormatException) { return 0; } catch { return -1; } }
    int Filtered(string x) { try { return int.Parse(x); } catch (System.Exception e) when (e.Message.Length > 0) { return 0; } }
    int Switch(int x) => x switch { 1 => 1, 2 => 2, _ => 0 };
    bool Or(bool a, bool b) => a || b;
    string Coalesce(string? a, string b) => a ?? b;
    int NullConditional(string? s) => s?.Length ?? 0;
    void CoalesceAssign(ref string? value) { value ??= ""; }
    int Loops(int[] values) { var n = 0; for (var i = 0; i < values.Length; i++) n++; foreach (var value in values) n += value; while (n < 0) n++; do n--; while (n > 0); return n; }
    bool Pattern(int value) => value is (> 0 and < 10) or 42;
    int Guard(int x) => x switch { > 0 when x % 2 == 0 => 1, > 0 => 2, _ => 0 };
    int Nested(int x) { System.Func<int, int> f = value => value > 0 ? 1 : 0; if (x > 0) return f(x); return 0; }
}
""";

var tree = CSharpSyntaxTree.ParseText(source);
var references = AppDomain.CurrentDomain.GetAssemblies()
    .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
    .Select(assembly => MetadataReference.CreateFromFile(assembly.Location));
var compilation = CSharpCompilation.Create("CfgProbe", [tree], references,
    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
var errors = compilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
if (errors.Length != 0)
    throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
var model = compilation.GetSemanticModel(tree);
foreach (var method in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
{
    var operation = model.GetOperation(method) as IMethodBodyOperation
        ?? throw new InvalidOperationException($"Missing method body: {method.Identifier.Text}");
    var graph = ControlFlowGraph.Create(operation);
    var sonar = CSharpCyclomaticComplexityMetric.GetComplexity(method).Complexity;
    var nodes = graph.Blocks.Where(block => block.IsReachable).ToDictionary(block => block.Ordinal);
    var edges = new List<(int From, int To, string Semantics)>();
    foreach (var block in nodes.Values)
    {
        Add(block.Ordinal, block.FallThroughSuccessor);
        Add(block.Ordinal, block.ConditionalSuccessor);
    }
    var components = Components(nodes.Keys, edges.Select(edge => (edge.From, edge.To)));
    var cc = edges.Count - nodes.Count + 2 * components;
    Console.WriteLine($"{method.Identifier.Text}: N={nodes.Count} E={edges.Count} P={components} rawCFG={cc} SonarCC={sonar}");
    foreach (var block in nodes.Values)
        Console.WriteLine($"  block {block.Ordinal} {block.Kind} region={Regions(block.EnclosingRegion)}");
    foreach (var edge in edges)
        Console.WriteLine($"  {edge.From}->{edge.To} {edge.Semantics}");

    void Add(int from, ControlFlowBranch? branch)
    {
        if (branch?.Destination is { } destination && nodes.ContainsKey(destination.Ordinal))
            edges.Add((from, destination.Ordinal, branch.Semantics.ToString()));
    }
}

foreach (var lambda in tree.GetRoot().DescendantNodes().OfType<AnonymousFunctionExpressionSyntax>())
    Console.WriteLine($"lambda@{lambda.SpanStart}: SonarCC={CSharpCyclomaticComplexityMetric.GetComplexity(lambda).Complexity} BodyCC={CSharpCyclomaticComplexityMetric.GetComplexity(lambda.Body).Complexity}");

static string Regions(ControlFlowRegion? region)
{
    var values = new List<string>();
    for (; region != null; region = region.EnclosingRegion)
        values.Add(region.Kind.ToString());
    return string.Join("/", values);
}

static int Components(IEnumerable<int> ordinals, IEnumerable<(int From, int To)> edges)
{
    var adjacent = ordinals.ToDictionary(value => value, _ => new HashSet<int>());
    foreach (var (from, to) in edges)
    {
        adjacent[from].Add(to);
        adjacent[to].Add(from);
    }
    var unseen = adjacent.Keys.ToHashSet();
    var count = 0;
    while (unseen.Count != 0)
    {
        count++;
        var pending = new Stack<int>([unseen.First()]);
        while (pending.TryPop(out var current))
            if (unseen.Remove(current))
                foreach (var next in adjacent[current])
                    pending.Push(next);
    }
    return count;
}

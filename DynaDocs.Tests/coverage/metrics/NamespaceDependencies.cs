using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GateMetrics;

public static class NamespaceDependencies
{
    public static IReadOnlyList<string[]> Collect(Compilation compilation, IEnumerable<SyntaxTree> trees)
    {
        var selected = trees.ToArray();
        var namespaces = selected.SelectMany(tree => tree.GetRoot().DescendantNodes()
            .OfType<BaseTypeDeclarationSyntax>()
            .Select(node => Name(compilation.GetSemanticModel(tree).GetDeclaredSymbol(node)?.ContainingNamespace)))
            .Where(name => name != null).ToHashSet();
        var edges = new HashSet<(string From, string To)>();
        foreach (var tree in selected)
            CollectTree(compilation.GetSemanticModel(tree), tree, namespaces, edges);
        return edges.OrderBy(edge => edge.From, StringComparer.Ordinal)
            .ThenBy(edge => edge.To, StringComparer.Ordinal)
            .Select(edge => new[] { edge.From, edge.To }).ToArray();
    }

    private static void CollectTree(SemanticModel model, SyntaxTree tree, HashSet<string?> namespaces,
        HashSet<(string From, string To)> edges)
    {
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>())
        {
            if (node.Ancestors().Any(parent => parent is UsingDirectiveSyntax))
                continue;
            var symbol = model.GetSymbolInfo(node).Symbol;
            if (symbol is not (ITypeSymbol or IMethodSymbol or IPropertySymbol or IFieldSymbol or IEventSymbol))
                continue;
            var from = Name(model.GetEnclosingSymbol(node.SpanStart)?.ContainingNamespace);
            var to = Name(symbol.ContainingNamespace);
            if (from != null && to != null && from != to && namespaces.Contains(from) && namespaces.Contains(to))
                edges.Add((from, to));
        }
    }

    private static string? Name(INamespaceSymbol? symbol)
    {
        if (symbol == null)
            return null;
        return symbol.IsGlobalNamespace ? "<global>" : symbol.ToDisplayString();
    }
}


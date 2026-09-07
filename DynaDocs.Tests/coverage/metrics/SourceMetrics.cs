using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SonarAnalyzer.CSharp.Metrics;

namespace GateMetrics;

public static class SourceMetrics
{
    public static IReadOnlyList<SourceToken> Tokens(SyntaxTree tree)
    {
        var root = tree.GetRoot();
        var spans = root.DescendantTokens().Where(token => !token.IsKind(SyntaxKind.EndOfFileToken))
            .Select(token => (token.Span, Kind: token.Kind().ToString(), Text: token.Text));
        var directives = root.DescendantTrivia().Where(trivia =>
            trivia.IsKind(SyntaxKind.DisabledTextTrivia) || trivia.IsDirective
            && !trivia.IsKind(SyntaxKind.RegionDirectiveTrivia) && !trivia.IsKind(SyntaxKind.EndRegionDirectiveTrivia))
            .Select(trivia => (trivia.Span, Kind: trivia.Kind().ToString(), Text: trivia.ToString()));
        return spans.Concat(directives).OrderBy(row => row.Span.Start).Select(row =>
        {
            var location = tree.GetLineSpan(row.Span);
            return new SourceToken(row.Kind, row.Text, location.StartLinePosition.Line + 1,
                location.StartLinePosition.Character, location.EndLinePosition.Line + 1, location.EndLinePosition.Character);
        }).ToArray();
    }

    public static IReadOnlyList<SourceMember> Measure(SyntaxTree tree)
    {
        var root = tree.GetRoot();
        var members = root.DescendantNodes().Where(IsCallable).Select(node => Describe(tree, node)).ToList();
        var globals = root.ChildNodes().OfType<GlobalStatementSyntax>().ToArray();
        if (globals.Length != 0)
            members.Insert(0, EntryPoint(tree, globals));
        return members;
    }

    private static SourceMember EntryPoint(SyntaxTree tree, GlobalStatementSyntax[] globals)
    {
        var body = string.Join("\n", globals.Select(item => item.ToFullString()));
        var wrapper = CSharpSyntaxTree.ParseText("class Entry { void Main() { " + body + " } }");
        var method = wrapper.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        var span = Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(globals[0].SpanStart, globals[^1].Span.End);
        var location = tree.GetLineSpan(span);
        return new SourceMember($"Program::<Main>$@{span.Start}:{span.Length}", "EntryPoint", "<Main>$", "Program",
            location.StartLinePosition.Line + 1, location.StartLinePosition.Character,
            location.EndLinePosition.Line + 1, location.EndLinePosition.Character, span.Start, span.Length,
            CSharpCognitiveComplexityMetric.GetComplexity(method).Complexity, 1, false);
    }

    private static bool IsCallable(SyntaxNode node) => node switch
    {
        BaseMethodDeclarationSyntax method => method.Body != null || method.ExpressionBody != null,
        AccessorDeclarationSyntax accessor => accessor.Body != null || accessor.ExpressionBody != null,
        PropertyDeclarationSyntax property => property.ExpressionBody != null,
        IndexerDeclarationSyntax indexer => indexer.ExpressionBody != null,
        LocalFunctionStatementSyntax => true,
        AnonymousFunctionExpressionSyntax => true,
        _ => false
    };

    private static SourceMember Describe(SyntaxTree tree, SyntaxNode node)
    {
        var location = tree.GetLineSpan(node.Span);
        var member = MemberName(node);
        var type = string.Join(".", node.Ancestors().Reverse().Select(ContainerName).Where(name => name != null));
        var id = $"{type}::{member}@{node.SpanStart}:{node.Span.Length}";
        return new SourceMember(id, node.Kind().ToString(), member, type,
            location.StartLinePosition.Line + 1, location.StartLinePosition.Character,
            location.EndLinePosition.Line + 1, location.EndLinePosition.Character,
            node.SpanStart, node.Span.Length, CSharpCognitiveComplexityMetric.GetComplexity(node).Complexity,
            ParameterCount(node), node is ConstructorDeclarationSyntax);
    }

    private static string? ContainerName(SyntaxNode node) => node switch
    {
        BaseNamespaceDeclarationSyntax space => space.Name.ToString(),
        TypeDeclarationSyntax type => type.Identifier.Text + (type.TypeParameterList?.ToString() ?? ""),
        MethodDeclarationSyntax method => method.Identifier.Text,
        LocalFunctionStatementSyntax local => local.Identifier.Text,
        _ => null
    };

    private static string MemberName(SyntaxNode node) => node switch
    {
        MethodDeclarationSyntax method => method.Identifier.Text,
        ConstructorDeclarationSyntax => ".ctor",
        DestructorDeclarationSyntax => "Finalize",
        OperatorDeclarationSyntax op => "operator " + op.OperatorToken.Text,
        ConversionOperatorDeclarationSyntax conversion => "operator " + conversion.Type,
        LocalFunctionStatementSyntax local => local.Identifier.Text,
        AccessorDeclarationSyntax accessor => accessor.Keyword.Text + "_" + accessor.Parent?.Parent?.ToString().Split('{')[0],
        PropertyDeclarationSyntax property => "get_" + property.Identifier.Text,
        IndexerDeclarationSyntax => "get_Item",
        AnonymousFunctionExpressionSyntax => "<lambda>",
        _ => throw new InvalidOperationException("Unsupported callable source node")
    };

    private static int ParameterCount(SyntaxNode node) => node switch
    {
        BaseMethodDeclarationSyntax method => method.ParameterList.Parameters.Count,
        LocalFunctionStatementSyntax local => local.ParameterList.Parameters.Count,
        ParenthesizedLambdaExpressionSyntax lambda => lambda.ParameterList.Parameters.Count,
        SimpleLambdaExpressionSyntax => 1,
        AnonymousMethodExpressionSyntax anonymous => anonymous.ParameterList?.Parameters.Count ?? 0,
        IndexerDeclarationSyntax indexer => indexer.ParameterList.Parameters.Count,
        _ => 0
    };
}

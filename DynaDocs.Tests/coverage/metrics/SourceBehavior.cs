using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SonarAnalyzer.CSharp.Metrics;

namespace GateMetrics;

/// <summary>Authored fragments and synthesized bodies are classified from semantic declarations.</summary>
public static class SourceBehavior
{
    public static BehaviorFacts Collect(Compilation compilation, IReadOnlyList<SyntaxTree> trees, string root)
    {
        var types = trees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>()
            .Select(node => compilation.GetSemanticModel(tree).GetDeclaredSymbol(node)))
            .OfType<INamedTypeSymbol>().Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var fragments = new Dictionary<string, BehaviorFragment>();
        var constructors = new List<ConstructorFacts>();
        var declared = new List<SemanticMethod>();
        var structural = new List<StructuralMethod>();
        foreach (var type in types)
            CollectType(type, trees, root, fragments, constructors, declared, structural);
        return new BehaviorFacts(fragments.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
            constructors, declared, structural);
    }

    private static void CollectType(INamedTypeSymbol type, IReadOnlyList<SyntaxTree> trees, string root,
        Dictionary<string, BehaviorFragment> fragments, List<ConstructorFacts> constructors,
        List<SemanticMethod> declared, List<StructuralMethod> structural)
    {
        var declarations = type.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax())
            .OfType<TypeDeclarationSyntax>().Where(node => trees.Contains(node.SyntaxTree))
            .OrderBy(node => IndexOf(trees, node.SyntaxTree)).ThenBy(node => node.SpanStart).ToArray();
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            var syntax = method.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax()).FirstOrDefault();
            if (syntax != null && !trees.Contains(syntax.SyntaxTree))
                continue;
            if (method.MethodKind is MethodKind.Constructor or MethodKind.StaticConstructor)
            {
                CollectConstructor(method, syntax, declarations, root, fragments, constructors, structural);
                continue;
            }
            if (method.IsAbstract || method.IsExtern || method.PartialImplementationPart != null)
                continue;
            if (method.IsImplicitlyDeclared || IsAutoAccessor(syntax))
            {
                structural.Add(new StructuralMethod(MethodIdentity.Key(method), "semantic synthesized member without authored body"));
                continue;
            }
            if (syntax != null)
                declared.Add(Declared(method, syntax, root));
        }
    }

    private static int IndexOf(IReadOnlyList<SyntaxTree> trees, SyntaxTree tree)
    {
        for (var index = 0; index < trees.Count; index++)
            if (trees[index] == tree)
                return index;
        throw new InvalidOperationException("Declaration tree missing from maintained compilation inventory.");
    }

    private static bool IsAutoAccessor(SyntaxNode? syntax) => syntax is AccessorDeclarationSyntax accessor
        && accessor.Body == null && accessor.ExpressionBody == null;

    private static SemanticMethod Declared(IMethodSymbol method, SyntaxNode syntax, string root)
    {
        var span = syntax.GetLocation().GetLineSpan();
        return new SemanticMethod(MethodIdentity.Key(method), Relative(root, syntax),
            span.StartLinePosition.Line + 1, span.StartLinePosition.Character,
            span.EndLinePosition.Line + 1, span.EndLinePosition.Character);
    }

    private static void CollectConstructor(IMethodSymbol method, SyntaxNode? syntax,
        TypeDeclarationSyntax[] declarations, string root, Dictionary<string, BehaviorFragment> fragments,
        List<ConstructorFacts> constructors, List<StructuralMethod> structural)
    {
        var authored = syntax as ConstructorDeclarationSyntax;
        var primary = PrimaryDeclaration(method, syntax, declarations);
        if (method.IsImplicitlyDeclared && method.ContainingType.IsRecord && method.Parameters.Length == 1
            && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, method.ContainingType))
        {
            structural.Add(new StructuralMethod(MethodIdentity.Key(method), "semantic synthesized record copy constructor"));
            return;
        }
        var initializers = Initializers(declarations, method.IsStatic, authored).ToArray();
        var key = MethodIdentity.Key(method);
        var owned = new List<string>();
        foreach (var initializer in initializers)
            owned.Add(AddFragment(fragments, initializer, "initializer", key, root));
        var clause = authored?.Initializer;
        var primaryBase = primary?.BaseList?.Types.OfType<PrimaryConstructorBaseTypeSyntax>().SingleOrDefault();
        if (clause != null)
            owned.Add(AddFragment(fragments, clause, "constructor-clause", key, root));
        if (primaryBase != null)
        {
            owned.Add(AddFragment(fragments, primaryBase.ArgumentList, "constructor-clause", key, root));
            clause = SyntaxFactory.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer, primaryBase.ArgumentList);
        }
        if (authored?.Body != null)
            owned.Add(AddFragment(fragments, authored.Body, "constructor-body", key, root));
        if (authored?.ExpressionBody != null)
            owned.Add(AddFragment(fragments, authored.ExpressionBody.Expression, "constructor-body", key, root));
        if (owned.Count == 0 && authored == null && primary == null)
        {
            structural.Add(new StructuralMethod(key, "semantic implicit constructor with no authored executable fragments"));
            return;
        }
        var statements = initializers.Select((expression, index) =>
            (StatementSyntax)SyntaxFactory.ParseStatement($"var __initializer{index} = {expression};")).ToList();
        if (authored?.Body != null)
            statements.AddRange(authored.Body.Statements);
        if (authored?.ExpressionBody != null)
            statements.Add(SyntaxFactory.ExpressionStatement(authored.ExpressionBody.Expression));
        var synthetic = SyntaxFactory.ConstructorDeclaration("Subject").WithInitializer(clause)
            .WithParameterList(authored?.ParameterList ?? primary?.ParameterList ?? SyntaxFactory.ParameterList())
            .WithBody(SyntaxFactory.Block(statements));
        constructors.Add(new ConstructorFacts(key, CSharpCognitiveComplexityMetric.GetComplexity(synthetic).Complexity,
            SourceMetrics.PolicyCc(synthetic), method.Parameters.Length, owned));
    }

    private static TypeDeclarationSyntax? PrimaryDeclaration(IMethodSymbol method, SyntaxNode? syntax,
        TypeDeclarationSyntax[] declarations)
    {
        if (syntax is TypeDeclarationSyntax type && type.ParameterList != null)
            return type;
        if (syntax is ParameterListSyntax parameters)
            return parameters.Parent as TypeDeclarationSyntax;
        if (method.IsImplicitlyDeclared || syntax is ConstructorDeclarationSyntax || method.IsStatic)
            return null;
        return declarations.SingleOrDefault(item => item.ParameterList != null);
    }

    private static IEnumerable<ExpressionSyntax> Initializers(TypeDeclarationSyntax[] declarations, bool isStatic,
        ConstructorDeclarationSyntax? constructor)
    {
        if (constructor?.Initializer?.ThisOrBaseKeyword.IsKind(SyntaxKind.ThisKeyword) == true)
            yield break;
        foreach (var member in declarations.SelectMany(declaration => declaration.Members))
        {
            if (member.Modifiers.Any(SyntaxKind.StaticKeyword) != isStatic || member.Modifiers.Any(SyntaxKind.ConstKeyword))
                continue;
            if (member is BaseFieldDeclarationSyntax field)
                foreach (var variable in field.Declaration.Variables.Where(variable => variable.Initializer != null))
                    yield return variable.Initializer!.Value;
            if (member is PropertyDeclarationSyntax { Initializer: not null } property)
                yield return property.Initializer.Value;
        }
    }

    private static string AddFragment(Dictionary<string, BehaviorFragment> fragments, SyntaxNode syntax,
        string kind, string owner, string root)
    {
        var path = Relative(root, syntax);
        var span = syntax.GetLocation().GetLineSpan();
        var start = span.StartLinePosition;
        var end = span.EndLinePosition;
        var id = $"{path}:{start.Line + 1}:{start.Character}-{end.Line + 1}:{end.Character}:{kind}";
        if (fragments.TryGetValue(id, out var previous))
            fragments[id] = previous with { Owners = previous.Owners.Append(owner).Distinct(StringComparer.Ordinal).ToArray() };
        else
            fragments.Add(id, new BehaviorFragment(id, path, kind, start.Line + 1, start.Character,
                end.Line + 1, end.Character, [owner]));
        return id;
    }

    private static string Relative(string root, SyntaxNode syntax)
    {
        var path = Path.GetRelativePath(root, syntax.SyntaxTree.FilePath).Replace('\\', '/');
        if (path == ".." || path.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(path))
            throw new InvalidOperationException("Source fragment outside maintained inventory: " + path);
        return path;
    }
}

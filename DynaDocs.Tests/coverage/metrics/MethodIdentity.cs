using Microsoft.CodeAnalysis;
using Mono.Cecil;

namespace GateMetrics;

/// <summary>A structural metadata identity shared by semantic and emitted methods.</summary>
public static class MethodIdentity
{
    public static string Key(IMethodSymbol method) =>
        $"{NamedDefinition(method.ContainingType)}::{method.MetadataName}`{method.Arity}({string.Join(",", method.Parameters.Select(parameter => Type(parameter.Type) + (parameter.RefKind == RefKind.None ? "" : "&")))})"
        + (method.MethodKind == MethodKind.Conversion ? "->" + Type(method.ReturnType) : "");

    public static string Key(MethodDefinition method) =>
        $"{method.DeclaringType.FullName}::{method.Name}`{method.GenericParameters.Count}({string.Join(",", method.Parameters.Select(parameter => Type(parameter.ParameterType)))})"
        + (method.Name is "op_Implicit" or "op_Explicit" or "op_CheckedExplicit" ? "->" + Type(method.ReturnType) : "");

    private static string NamedDefinition(INamedTypeSymbol type)
    {
        if (type.ContainingType != null)
            return NamedDefinition(type.ContainingType) + "/" + type.MetadataName;
        var prefix = type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString() + ".";
        return prefix + type.MetadataName;
    }

    private static string Type(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol array => Type(array.ElementType) + "[" + new string(',', array.Rank - 1) + "]",
        IPointerTypeSymbol pointer => Type(pointer.PointedAtType) + "*",
        ITypeParameterSymbol parameter => (parameter.TypeParameterKind == TypeParameterKind.Method ? "!!" : "!") + ParameterPosition(parameter),
        IDynamicTypeSymbol => "System.Object",
        INamedTypeSymbol named => NamedDefinition(named) + Arguments(named),
        _ => throw new InvalidOperationException($"Unsupported semantic parameter type: {type.Kind} {type}")
    };

    private static int ParameterPosition(ITypeParameterSymbol parameter)
    {
        var position = parameter.Ordinal;
        if (parameter.TypeParameterKind == TypeParameterKind.Type)
            for (var parent = parameter.ContainingType.ContainingType; parent != null; parent = parent.ContainingType)
                position += parent.Arity;
        return position;
    }

    private static string Arguments(INamedTypeSymbol type)
    {
        var arguments = new List<ITypeSymbol>();
        for (var current = type; current != null; current = current.ContainingType)
            arguments.InsertRange(0, current.TypeArguments);
        return arguments.Count == 0 ? "" : "<" + string.Join(",", arguments.Select(Type)) + ">";
    }

    private static string Type(TypeReference type) => type switch
    {
        ArrayType array => Type(array.ElementType) + "[" + new string(',', array.Rank - 1) + "]",
        PointerType pointer => Type(pointer.ElementType) + "*",
        ByReferenceType reference => Type(reference.ElementType) + "&",
        GenericParameter parameter => (parameter.Type == GenericParameterType.Method ? "!!" : "!") + parameter.Position,
        GenericInstanceType generic => generic.ElementType.FullName + "<" + string.Join(",", generic.GenericArguments.Select(Type)) + ">",
        RequiredModifierType modifier => Type(modifier.ElementType),
        OptionalModifierType modifier => Type(modifier.ElementType),
        FunctionPointerType => throw new InvalidOperationException("Function-pointer parameter identity is not implemented."),
        _ => type.FullName
    };
}


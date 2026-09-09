using Mono.Cecil;
using System.Security.Cryptography;

namespace GateMetrics;

public static class AssemblyMetrics
{
    public static AssemblyFacts Collect(string assemblyPath, string root, string projectPath)
    {
        assemblyPath = Path.GetFullPath(assemblyPath);
        projectPath = Path.GetFullPath(projectPath);
        var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
        if (!File.Exists(assemblyPath) || !File.Exists(pdbPath))
            throw new InvalidOperationException("Exact assembly and portable PDB are required.");
        if (!File.Exists(projectPath))
            throw new InvalidOperationException("Exact owning project is required.");
        using var assembly = AssemblyDefinition.ReadAssembly(assemblyPath,
            new ReaderParameters { ReadSymbols = true, ThrowIfSymbolsAreNotMatching = true });
        var documents = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var methods = Types(assembly.MainModule.Types).SelectMany(type => type.Methods)
            .Where(method => method.HasBody).Select(method => Describe(method, root, projectPath, documents)).ToArray();
        if (methods.Select(method => method.Token).Distinct().Count() != methods.Length)
            throw new InvalidOperationException("Duplicate method token in assembly.");
        var relative = Path.GetRelativePath(Path.GetFullPath(root), assemblyPath).Replace('\\', '/');
        if (relative == ".." || relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new InvalidOperationException("Assembly outside inventory root.");
        return new AssemblyFacts(assembly.Name.Name, relative, new FileInfo(assemblyPath).Length,
            Convert.ToHexStringLower(SHA1.HashData(File.ReadAllBytes(assemblyPath))),
            Hash(assemblyPath), Hash(pdbPath), assembly.MainModule.Mvid.ToString(), documents, methods);
    }

    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    private static IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> types)
    {
        foreach (var type in types)
        {
            yield return type;
            foreach (var nested in Types(type.NestedTypes))
                yield return nested;
        }
    }

    private static CompiledMethod Describe(MethodDefinition method, string root, string projectPath,
        IDictionary<string, string> documents)
    {
        var points = method.DebugInformation.SequencePoints.Where(point => !point.IsHidden)
            .Select(point => Point(point, root, projectPath, documents)).ToArray();
        var generated = method.CustomAttributes.Concat(method.DeclaringType.CustomAttributes)
            .Any(attribute => attribute.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");
        var kickoff = method.DebugInformation.StateMachineKickOffMethod;
        return new CompiledMethod(method.MetadataToken.ToInt32(), method.FullName, MethodIdentity.Key(method),
            kickoff?.FullName, kickoff == null ? null : MethodIdentity.Key(kickoff), generated, points);
    }

    private static SourcePoint Point(Mono.Cecil.Cil.SequencePoint point, string root, string projectPath,
        IDictionary<string, string> documents)
    {
        var identity = ProjectMetrics.SourceIdentity(Path.GetFullPath(root), projectPath, point.Document.Url,
            "PDB source");
        if (documents.TryGetValue(point.Document.Url, out var existing) && existing != identity.Path)
            throw new InvalidOperationException($"Conflicting PDB document identity: {point.Document.Url}");
        documents[point.Document.Url] = identity.Path;
        if (point.StartLine < 1 || point.StartColumn < 1 || point.EndColumn < 1)
            throw new InvalidOperationException($"Invalid PDB source position: {identity.Path}");
        return new SourcePoint(identity.Path, identity.Origin, point.Offset, point.StartLine, point.StartColumn - 1,
            point.EndLine, point.EndColumn - 1, point.Document.HashAlgorithm.ToString(),
            Convert.ToHexStringLower(point.Document.Hash));
    }
}

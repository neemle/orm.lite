using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Mig = Neemle.OrmLite.MigrationGenerator.Mig;

namespace Neemle.OrmLite;

/// <summary>
/// The generated migration list builds each [Migration] class with MigrationContribution.For of the class, which
/// needs a class the generated file can name that derives from Neemle.OrmLite.Migration and has a public
/// constructor without parameters. A class that lacks one of these is reported with ORMLITE030 and not emitted,
/// instead of failing with CS0310 inside the generated file.
/// </summary>
internal static class MigrationShape
{
    private const string MigrationBase = "Neemle.OrmLite.Migration";

    private static readonly DiagnosticDescriptor Unusable = new DiagnosticDescriptor(
        id: "ORMLITE030",
        title: "A [Migration] class cannot be used by the generated code",
        messageFormat:
            "[Migration(\"{1}\")] on '{0}' cannot be emitted because {2}. Declare a non-static, non-abstract class "
            + "that is not generic, is public or internal, derives from Neemle.OrmLite.Migration and has a public "
            + "constructor without parameters. The migration is not emitted.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>A class, or a record class (which cannot derive from Migration and is reported).</summary>
    internal static bool IsDeclaration(SyntaxNode node) =>
        node is ClassDeclarationSyntax
        || (node is RecordDeclarationSyntax && node.RawKind == (int)SyntaxKind.RecordDeclaration);

    internal static void Report(SourceProductionContext spc, ImmutableArray<Mig> migs)
    {
        foreach (var m in migs.Where(m => m.Shape is not null))
            spc.ReportDiagnostic(Diagnostic.Create(Unusable, m.Location, m.Type, m.Version, m.Shape));
    }

    /// <summary>Why the class cannot be built by the generated code, or null when it can.</summary>
    internal static string? IssueOf(INamedTypeSymbol type)
    {
        var chain = Enclosing(type);
        if (chain.Any(t => t.IsGenericType))
            return "it is generic, or nested in a generic type, and the generated code cannot supply type arguments";
        if (chain.Any(t => t.IsFileLocal)) return "it is file-local, which the generated file cannot name";
        if (!chain.All(t => Reachable(t.DeclaredAccessibility)))
            return "it, or a type it is nested in, is private, protected or private protected, which the generated "
                + "code cannot name";
        if (type.IsStatic) return "it is a static class";
        if (type.IsAbstract) return "it is abstract";
        if (!Derives(type)) return "it does not derive from " + MigrationBase;
        var parameterless = type.InstanceConstructors
            .Any(c => c.DeclaredAccessibility == Accessibility.Public && c.Parameters.Length == 0);
        return parameterless ? null : "it has no public constructor without parameters";
    }

    private static ImmutableArray<INamedTypeSymbol> Enclosing(INamedTypeSymbol type)
    {
        var chain = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        for (var t = type; t is not null; t = t.ContainingType) chain.Add(t);
        return chain.ToImmutable();
    }

    private static bool Reachable(Accessibility accessibility) =>
        accessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal;

    private static bool Derives(INamedTypeSymbol type)
    {
        for (var t = type.BaseType; t is not null; t = t.BaseType)
            if (string.Equals(t.ToDisplayString(), MigrationBase, StringComparison.Ordinal)) return true;
        return false;
    }
}

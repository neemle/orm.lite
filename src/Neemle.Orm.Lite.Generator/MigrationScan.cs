using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Neemle.Orm.Lite;

internal sealed record MigrationScan(
    EquatableArray<string> Lossy,
    EquatableArray<string> Unknown,
    int ReversibleCount,
    bool Opaque)
{
    private static readonly HashSet<string> ReversibleNames = new(StringComparer.Ordinal)
    {
        "CreateTable", "AddColumn", "AddDecimalColumn", "AddJsonbColumn", "CreateIndex", "CreateUniqueIndex",
        "RenameColumn", "AddForeignKey", "CreateView", "CreateViews",
    };

    private static readonly HashSet<string> LossyNames = new(StringComparer.Ordinal)
    {
        "DropTable", "DropColumn", "DropIndex", "DropForeignKey", "DeleteData", "DropView", "DropViews",
    };

    private static readonly HashSet<string> UnknownNames = new(StringComparer.Ordinal)
    {
        "Raw", "InsertData", "ReplaceView",
    };

    private static readonly HashSet<string> Helpers = new(StringComparer.Ordinal)
    {
        "ForeignKeyActionDdl", "LiveOnlyConversionDdl", "SoftDeleteColumnDdl",
    };

    internal static readonly MigrationScan OpaqueScan = new([], [], 0, true);

    internal static MigrationScan Of(INamedTypeSymbol type)
    {
        if (UpMethod(type) is not { } method) return OpaqueScan;
        var builder = method.ParameterList.Parameters[0].Identifier.ValueText;
        var opaque = method.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Any(i => i.Identifier.ValueText == builder
                && !(i.Parent is MemberAccessExpressionSyntax m && m.Expression == i));
        var tally = new Tally();
        foreach (var call in method.DescendantNodes().OfType<InvocationExpressionSyntax>().OrderBy(c => c.Span.End))
            tally.Add(call, builder);
        return new MigrationScan([.. tally.Lossy], [.. tally.Unknown], tally.Reversible, opaque);
    }

    private static MethodDeclarationSyntax? UpMethod(INamedTypeSymbol type)
    {
        var up = type.GetMembers("Up").OfType<IMethodSymbol>().FirstOrDefault(m =>
            m.IsOverride && m.Parameters.Length == 1
            && m.Parameters[0].Type.ToDisplayString() == "Neemle.Orm.Lite.MigrationBuilder");
        if (up?.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax method
            || method.ParameterList.Parameters.Count != 1)
            return null;
        return method;
    }

    /// <summary>The calls on the migration builder, sorted into those with an automatic inverse and the rest.</summary>
    private sealed class Tally
    {
        public List<string> Lossy { get; } = new();

        public List<string> Unknown { get; } = new();

        public int Reversible { get; private set; }

        public void Add(InvocationExpressionSyntax call, string builder)
        {
            var name = (call.Expression as MemberAccessExpressionSyntax)?.Name.Identifier.ValueText;
            if (name is null || RootOf(call) != builder) return;
            if (name == "Add") AddHelper(call, name);
            else if (ReversibleNames.Contains(name)) Reversible++;
            else if (LossyNames.Contains(name)) Lossy.Add(name);
            else if (UnknownNames.Contains(name)) Unknown.Add(name);
        }

        private void AddHelper(InvocationExpressionSyntax call, string name)
        {
            var arguments = call.ArgumentList.Arguments;
            if (arguments.Count == 1 && IsHelper(arguments[0].Expression)) Reversible++;
            else Unknown.Add(name);
        }
    }

    private static bool IsHelper(ExpressionSyntax expression) =>
        expression is InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax owner },
        }
        && Helpers.Contains(owner.Identifier.ValueText);

    private static string? RootOf(ExpressionSyntax expression)
    {
        while (true)
        {
            switch (expression)
            {
                case InvocationExpressionSyntax invocation:
                    expression = invocation.Expression;
                    break;
                case MemberAccessExpressionSyntax access:
                    expression = access.Expression;
                    break;
                case IdentifierNameSyntax identifier:
                    return identifier.Identifier.ValueText;
                default:
                    return null;
            }
        }
    }
}

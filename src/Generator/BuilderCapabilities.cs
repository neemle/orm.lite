using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Neemle.OrmLite.Generator;

/// <summary>
/// The capabilities that migration members need, for ORMLITE016. [RequiresCapability] cannot say what a call needs
/// when the need depends on an argument (AddColumn with ColumnType.Decimal), and the runtime types that carry the
/// migration surface are shared with other changes, so the list lives here, next to the analyzer that reads it. It
/// mirrors what the nodes of those members ask the provider for when they are rendered (Node.Requires), so a member
/// listed here is refused at build time for exactly the providers that would refuse it at run time.
/// </summary>
internal static class BuilderCapabilities
{
    private const string Lite = "Neemle.OrmLite.";

    private const string TypeParameter = "type";

    private static readonly Dictionary<string, string[]> Members = new(StringComparer.Ordinal)
    {
        [Lite + "MigrationBuilder.AddForeignKey"] = ["AlterConstraint"],
        [Lite + "MigrationBuilder.DropForeignKey"] = ["AlterConstraint"],
        [Lite + "MigrationBuilder.CreateView"] = ["ObjectMarker"],
        [Lite + "MigrationBuilder.ReplaceView"] = ["ObjectMarker"],
        [Lite + "MigrationBuilder.DropView"] = ["ObjectMarker"],
        [Lite + "MigrationBuilder.CreateViews"] = ["ObjectMarker"],
        [Lite + "MigrationBuilder.DropViews"] = ["ObjectMarker"],
        [Lite + "MigrationBuilder.AddDecimalColumn"] = ["ExactDecimal"],
        [Lite + "MigrationBuilder.AddJsonbColumn"] = ["JsonDocument"],
        [Lite + "LiveOnlyConversionDdl.Up"] = ["PartialIndex", "AlterConstraint", "ObjectMarker"],
        [Lite + "LiveOnlyConversionDdl.Down"] = ["PartialIndex", "AlterConstraint", "ObjectMarker"],
        [Lite + "ForeignKeyActionDdl.Up"] = ["AlterConstraint"],
        [Lite + "ForeignKeyActionDdl.Down"] = ["AlterConstraint"],
        [Lite + "SoftDeleteColumnDdl.Up"] = ["ObjectMarker"],
        [Lite + "SoftDeleteColumnDdl.Down"] = ["ObjectMarker"],
        [Lite + "IndexTerm.NullsFirst"] = ["IndexNullsOrdering"],
        [Lite + "IndexTerm.NullsLast"] = ["IndexNullsOrdering"],
        [Lite + "IndexTerm.Collate"] = ["IndexCollation"],
        [Lite + "IndexTerm.LowerOf"] = ["IndexExpression"],
    };

    private static readonly Dictionary<string, string> ColumnTypes = new(StringComparer.Ordinal)
    {
        ["Json"] = "JsonDocument",
        ["Decimal"] = "ExactDecimal",
    };

    internal static IEnumerable<string> Of(IInvocationOperation invocation)
    {
        var method = invocation.TargetMethod.OriginalDefinition;
        var key = method.ContainingType.ToDisplayString() + "." + method.Name;
        if (Members.TryGetValue(key, out var needs)) return needs;
        return key == Lite + "MigrationBuilder.AddColumn" ? ColumnTypeNeeds(invocation) : [];
    }

    private static IEnumerable<string> ColumnTypeNeeds(IInvocationOperation invocation)
    {
        var argument = invocation.Arguments.FirstOrDefault(a => a.Parameter?.Name == TypeParameter);
        if (argument is null || !argument.Value.ConstantValue.HasValue) return [];
        var written = argument.Value.ConstantValue.Value;
        var member = argument.Value.Type?.GetMembers().OfType<IFieldSymbol>()
            .FirstOrDefault(f => f.HasConstantValue && Equals(f.ConstantValue, written));
        return member is not null && ColumnTypes.TryGetValue(member.Name, out var need) ? [need] : [];
    }
}

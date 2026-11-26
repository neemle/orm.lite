using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Neemle.OrmLite.Generator;

/// <summary>Activates migration layout only for actual FK intent attributes, preserving the legacy byte path.</summary>
internal static class ForeignKeyMigrationLayout
{
    internal static IncrementalValueProvider<bool> Enabled(IncrementalGeneratorInitializationContext context) =>
        Intents(context, "Neemle.OrmLite.ForeignKeyAttribute")
            .Combine(Intents(context, "Neemle.OrmLite.LinkForeignKeyAttribute"))
            .Select(static (pair, _) => !pair.Left.IsEmpty || !pair.Right.IsEmpty);

    private static IncrementalValueProvider<ImmutableArray<bool>> Intents(
        IncrementalGeneratorInitializationContext context, string name) =>
        context.SyntaxProvider.ForAttributeWithMetadataName(name,
            static (_, _) => true, static (_, _) => true).Collect();

    internal static string Format(string source, bool hasForeignKeys) =>
        hasForeignKeys ? new ForeignKeyLineLayout().Format(source) : source;
}

using SourceLayout = Neemle.OrmLite.Generator.GeneratedSourceLayout.GeneratedSourceLayout;
using Microsoft.CodeAnalysis;

namespace Neemle.OrmLite.Generator;

/// <summary>Keeps graph generation on the shared numeric source layout and Map-preserving formatter.</summary>
internal static class GraphSourceLayout
{
    internal static void Emit(SourceProductionContext context, string source)
    {
        foreach (var (hint, text) in SourceLayout.Entities(source)) context.AddSource(hint, text);
    }
}

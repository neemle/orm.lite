using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Neemle.Orm.Lite.Tests.Gates;

namespace Neemle.Orm.Lite.Tests.Ports;

// Counts, per source file of the provider-neutral assemblies, the tokens that match a coupling marker. Comments are
// not tokens of either kind, so prose that names a provider is not counted; a string literal is counted by its text.
internal static class CoreCouplingScan
{
    internal static readonly string[] Areas = ["src/Neemle.Orm.Lite.Runtime", "src/Neemle.Orm.Lite.Sql"];

    internal static SortedDictionary<string, int> Of(string source)
    {
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var token in CSharpSyntaxTree.ParseText(source).GetRoot().DescendantTokens())
        {
            if (Classify(token) is not { } kind) continue;
            foreach (var marker in CouplingMarkers.All.Where(m => m.Matches(kind, token.ValueText)))
                counts[marker.Id] = (counts.TryGetValue(marker.Id, out var seen) ? seen : 0) + 1;
        }
        return counts;
    }

    internal static IReadOnlyList<string> Files(string root) =>
        Areas.SelectMany(area => LiteralScan.SourceFiles(Path.Combine(root, area))).ToList();

    // The key of an entry is "<file relative to the library root>|<marker id>".
    internal static SortedDictionary<string, int> OfTree(string root)
    {
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var file in Files(root))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            foreach (var (marker, count) in Of(File.ReadAllText(file))) counts[relative + "|" + marker] = count;
        }
        return counts;
    }

    private static CouplingTokenKind? Classify(SyntaxToken token) => token.Kind() switch
    {
        SyntaxKind.IdentifierToken => CouplingTokenKind.Identifier,
        SyntaxKind.StringLiteralToken
            or SyntaxKind.InterpolatedStringTextToken
            or SyntaxKind.SingleLineRawStringLiteralToken
            or SyntaxKind.MultiLineRawStringLiteralToken => CouplingTokenKind.Literal,
        _ => null,
    };
}

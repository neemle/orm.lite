using Microsoft.CodeAnalysis.CSharp;

namespace Neemle.OrmLite.Generator;

internal static class PolicyCode
{
    internal const string Runtime = "global::Neemle.OrmLite.";
    internal const string Task = "global::System.Threading.Tasks.Task";
    internal const string Token = "global::System.Threading.CancellationToken";
    internal const string Options = Runtime + "RelationReadOptions options, " + Token + " ct = default";
    internal static string Identifier(string value) => SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None
        || SyntaxFacts.GetContextualKeywordKind(value) != SyntaxKind.None ? "@" + value : value;
    internal static string Literal(string value) => SymbolDisplay.FormatLiteral(value, true);
}

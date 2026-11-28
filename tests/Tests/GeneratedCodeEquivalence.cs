using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Neemle.OrmLite.Tests;

/// <summary>
/// Judges two pieces of generated C# as the same code or not. Neemle.CSharpComposer renders through its own formatter
/// and promises code equivalence, not the same bytes, so a golden of generated source is compared here rather than as
/// text.
/// </summary>
/// <remarks>
/// <para>
/// The rule. Both sources must parse as C# without an error. Each is then read, in source order, as one run of units:
/// every token is a unit, and every trivia other than whitespace and line ends is a unit at its place between the
/// tokens. Two sources are the same code if and only if their runs are equal unit for unit, where two units are equal
/// when they have the same Roslyn kind and the same text. Whitespace and line ends are the only things ignored; nothing
/// is compared on the side, so a directive or a comment that moves past a token is a difference.
/// </para>
/// <para>
/// The text of a unit:
/// <list type="bullet">
/// <item>A token: its source text, so a changed literal, operator, keyword or literal form (<c>"a"</c> against
/// <c>@"a"</c>, <c>10L</c> against <c>10</c>) is a difference. An identifier is the one exception, below.</item>
/// <item>A directive (<c>#nullable</c>, <c>#pragma</c>, <c>#if</c>, <c>#region</c> and the rest) is code: it is read
/// as its own tokens and trivia under this same rule, so <c>#if (a&amp;&amp;b)</c> and <c>#if (a &amp;&amp; b)</c>
/// are the same, while another symbol, warning number or nullable setting is not.</item>
/// <item>Disabled text, the inactive branch of an <c>#if</c>, is code the compiler skipped only because of the symbols
/// of this parse: it is lexed as C# and read under this same rule, so a string literal in it keeps its spaces and a
/// line end in it still ends a <c>//</c> comment.</item>
/// <item>A comment is prose: its kind, and its content without the delimiters (<c>//</c>, <c>/* */</c>, and the
/// <c>///</c> or <c>*</c> line markers of a documentation comment) with every run of whitespace made one space and
/// the ends trimmed. Re-wrapping a comment, or <c>//x</c> against <c>// x</c>, is formatting; another word is not.
/// Any other trivia that is not whitespace (a <c>#region</c> name, for one) is compared the same way.</item>
/// </list>
/// </para>
/// <para>
/// Identifiers and the verbatim <c>@</c>. C# defines <c>@x</c> and <c>x</c> as the same identifier: the <c>@</c> only
/// lets a keyword be used as a name. So an identifier is compared by its value, <c>@Z</c> as <c>Z</c>, wherever that
/// cannot change what the code means. A reserved keyword written with <c>@</c> (<c>@class</c>) is an identifier token
/// against a keyword token, so the kind already tells them apart. A contextual keyword the parser reads as a keyword
/// where it acts as one (<c>where</c>, <c>async</c>, <c>get</c>, <c>await</c>, ...) becomes a keyword token there, so
/// the kind tells those apart too, and where it stays an identifier token, as the <c>@where</c> parameter of a
/// generated purge method does, it is only a name. What is left are the words the parser leaves as identifiers and the
/// compiler reads by their spelling while binding: <c>SpelledWords</c>. Bare, <c>var</c> is the implicit type,
/// <c>nint</c> a native integer, <c>unmanaged</c> or <c>notnull</c> a constraint, <c>nameof</c> the operator,
/// <c>_</c> a discard and <c>field</c> the backing field of C# 14 (which this Roslyn still parses as a name); with the
/// <c>@</c> each always names a declared symbol, and which reading applies depends on what is in scope, which syntax
/// cannot see. For these words the spelling is compared, <c>@</c> included. <c>dynamic</c> is not one of them: the
/// compiler binds <c>@dynamic</c> as <c>dynamic</c>. The other place an <c>@</c> counts is the name of an
/// attribute: bare, the compiler looks up <c>[Foo]</c> as <c>FooAttribute</c> too, and the <c>@</c> turns that lookup
/// off, so <c>[@Foo]</c> names only a type called <c>Foo</c>. The rightmost identifier of an attribute's name
/// (<c>Foo</c> in <c>[Foo]</c>, <c>[A.Foo]</c>, <c>[global::Foo]</c> or <c>[Foo&lt;int&gt;]</c>) is compared by its
/// spelling, <c>@</c> included.
/// </para>
/// </remarks>
public static class GeneratedCodeEquivalence
{
    private static readonly CSharpParseOptions Parse = new(LanguageVersion.Latest);

    // The words whose verbatim `@` changes what the code means; see the remarks above.
    private static readonly HashSet<string> SpelledWords = new(StringComparer.Ordinal)
        { "var", "nint", "nuint", "unmanaged", "notnull", "nameof", "_", "field" };

    // Null when the two sources are the same code; otherwise where they first part, in the words of a test failure.
    public static string? Difference(string expected, string actual)
    {
        var golden = CSharpSyntaxTree.ParseText(expected, Parse);
        var generated = CSharpSyntaxTree.ParseText(actual, Parse);
        var unparsable = Errors(golden, "golden") ?? Errors(generated, "generated");
        if (unparsable is not null) return unparsable;

        var left = Units(golden);
        var right = Units(generated);
        for (var i = 0; i < Math.Max(left.Count, right.Count); i++)
        {
            Unit? l = i < left.Count ? left[i] : null;
            Unit? r = i < right.Count ? right[i] : null;
            if (l is { } a && r is { } b && a.Kind == b.Kind && a.Text == b.Text) continue;
            return "not the same code: golden " + Describe(golden, l) + ", generated " + Describe(generated, r);
        }
        return null;
    }

    public static bool AreEquivalent(string expected, string actual) => Difference(expected, actual) is null;

    private static string? Errors(SyntaxTree tree, string side)
    {
        var error = tree.GetDiagnostics().FirstOrDefault(d => d.Severity == DiagnosticSeverity.Error);
        return error is null ? null : "the " + side + " source does not parse as C#: " + error;
    }

    private static List<Unit> Units(SyntaxTree tree)
    {
        var units = new List<Unit>();
        Read(tree.GetRoot().DescendantTokens(descendIntoTrivia: false), units);
        return units;
    }

    // The one walk: each token with the trivia around it, in order. The end-of-file token has no text; its trivia does.
    private static void Read(IEnumerable<SyntaxToken> tokens, List<Unit> units)
    {
        foreach (var token in tokens)
        {
            foreach (var trivia in token.LeadingTrivia) Read(trivia, units);
            if (!token.IsKind(SyntaxKind.EndOfFileToken)) units.Add(new Unit(token.Kind(), TextOf(token), token.Text, token.SpanStart));
            foreach (var trivia in token.TrailingTrivia) Read(trivia, units);
        }
    }

    private static void Read(SyntaxTrivia trivia, List<Unit> units)
    {
        var kind = trivia.Kind();
        if (kind is SyntaxKind.WhitespaceTrivia or SyntaxKind.EndOfLineTrivia) return;
        if (kind == SyntaxKind.DisabledTextTrivia)
            Read(SyntaxFactory.ParseTokens(trivia.ToFullString(), initialTokenPosition: trivia.SpanStart, options: Parse), units);
        else if (trivia.HasStructure && !SyntaxFacts.IsDocumentationCommentTrivia(kind))
            Read(trivia.GetStructure()!.DescendantTokens(descendIntoTrivia: false), units);
        else
            units.Add(new Unit(kind, Collapse(Content(trivia)), Collapse(trivia.ToFullString()), trivia.SpanStart));
    }

    private static string TextOf(SyntaxToken token) =>
        token.IsKind(SyntaxKind.IdentifierToken) && !SpelledWords.Contains(token.ValueText) && !NamesAnAttribute(token)
            ? token.ValueText
            : token.Text;

    // The rightmost identifier of an attribute's name, where the `@` turns off the `Attribute`-suffix lookup.
    private static bool NamesAnAttribute(SyntaxToken token) =>
        token.Parent is SimpleNameSyntax name && name.Identifier == token && name.Parent switch
        {
            AttributeSyntax attribute => attribute.Name == name,
            QualifiedNameSyntax qualified =>
                qualified.Right == name && qualified.Parent is AttributeSyntax attribute && attribute.Name == qualified,
            AliasQualifiedNameSyntax alias =>
                alias.Name == name && alias.Parent is AttributeSyntax attribute && attribute.Name == alias,
            _ => false,
        };

    // A comment without its delimiters; a documentation comment without its `///` or `*` line markers.
    private static string Content(SyntaxTrivia trivia)
    {
        var text = trivia.ToFullString();
        switch (trivia.Kind())
        {
            case SyntaxKind.SingleLineCommentTrivia:
                return text[2..];
            case SyntaxKind.MultiLineCommentTrivia:
                text = text[2..];
                return text.EndsWith("*/", StringComparison.Ordinal) ? text[..^2] : text;
            case SyntaxKind.SingleLineDocumentationCommentTrivia or SyntaxKind.MultiLineDocumentationCommentTrivia:
                var content = new StringBuilder(text.Length);
                foreach (var token in trivia.GetStructure()!.DescendantTokens(descendIntoTrivia: false))
                {
                    Append(content, token.LeadingTrivia);
                    content.Append(token.Text);
                    Append(content, token.TrailingTrivia);
                }
                return content.ToString();
            default:
                return text;
        }
    }

    private static void Append(StringBuilder content, SyntaxTriviaList trivia)
    {
        foreach (var item in trivia)
            if (!item.IsKind(SyntaxKind.DocumentationCommentExteriorTrivia)) content.Append(item.ToFullString());
    }

    private static string Collapse(string text)
    {
        var builder = new StringBuilder(text.Length);
        var space = false;
        foreach (var c in text.Trim())
        {
            if (char.IsWhiteSpace(c)) { space = true; continue; }
            if (space) builder.Append(' ');
            builder.Append(c);
            space = false;
        }
        return builder.ToString();
    }

    private static string Describe(SyntaxTree tree, Unit? unit) =>
        unit is not { } u ? "<end of file>" : "line " + (tree.GetText().Lines.GetLinePosition(u.Position).Line + 1) + " `" + Clip(u.Shown) + "`";

    private static string Clip(string text) => text.Length <= 120 ? text : text[..120] + "...";

    // Kind and Text are what is compared; Shown and Position only say where a difference is.
    private readonly record struct Unit(SyntaxKind Kind, string Text, string Shown, int Position);
}

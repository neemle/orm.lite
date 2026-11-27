using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Neemle.OrmLite.Tests.Gates;

// Finds the Composer escapes the generator must not use: a Raw* call, parsing text into a document (Parse, ParseTree)
// or reformatting one (Reformat). It reads syntax trees, so a comment or line break between receiver and member hides
// nothing, and it resolves every such name against Neemle.CSharpComposer, so an alias (using C = ...Cs) or a static
// import (using static ...Cs) reaches the same method and is reported. Where a name does not resolve (a snippet with no
// using, an unknown receiver), the names keep the spelling rule the regex guard had: any invoked Raw* or Reformat, and
// Parse or ParseTree invoked on a receiver spelled Cs.
internal static class ComposerEscapeScan
{
    private const string ComposerAssembly = "Neemle.CSharpComposer";

    private static readonly Regex RawName = new(@"\ARaw[A-Z][A-Za-z]*\z", RegexOptions.CultureInvariant);

    private static readonly Lazy<MetadataReference[]> References =
        new(BuildReferences, LazyThreadSafetyMode.ExecutionAndPublication);

    internal static IReadOnlyList<string> Find(IEnumerable<(string Path, string Text)> sources)
    {
        var options = new CSharpParseOptions(LanguageVersion.Latest);
        var trees = sources.Select(s => CSharpSyntaxTree.ParseText(s.Text, options, s.Path)).ToArray();
        var compilation = CSharpCompilation.Create("ComposerEscapeScan", trees, References.Value,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));

        var hits = new List<string>();
        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var name in tree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>())
            {
                if (IsEscape(name, model))
                {
                    var line = name.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    var shown = (name.Parent is MemberAccessExpressionSyntax access ? access : (SyntaxNode)name).ToString();
                    hits.Add(Path.GetFileName(tree.FilePath) + ":" + line + ": " + shown);
                }
            }
        }

        return hits;
    }

    private static bool IsEscape(SimpleNameSyntax name, SemanticModel model)
    {
        var text = name.Identifier.ValueText;
        var raw = RawName.IsMatch(text) || text == "Reformat";
        var parse = text is "Parse" or "ParseTree";
        if (!raw && !parse) return false;

        var info = model.GetSymbolInfo(name);
        var symbols = info.Symbol is { } symbol ? [symbol] : info.CandidateSymbols;
        if (symbols.Any(s => s is IMethodSymbol && s.ContainingAssembly?.Name == ComposerAssembly)) return true;

        var invocation = InvokedThrough(name);
        if (invocation is null) return false;
        if (raw) return true;
        return name.Parent is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "Cs" } };
    }

    // The invocation that calls this name directly (Parse(x)), through a member access (a.Parse(x)) or through a
    // conditional access (a?.Parse(x)); null when the name is not what is being invoked.
    private static InvocationExpressionSyntax? InvokedThrough(SimpleNameSyntax name)
    {
        SyntaxNode callee = name.Parent is MemberAccessExpressionSyntax access && access.Name == name ? access
            : name.Parent is MemberBindingExpressionSyntax binding && binding.Name == name ? binding
            : name;
        return callee.Parent is InvocationExpressionSyntax invocation && invocation.Expression == callee ? invocation : null;
    }

    private static MetadataReference[] BuildReferences()
    {
        // Composer is referenced explicitly: a gate that silently lost it would resolve nothing and pass.
        var composer = Assembly.Load(new AssemblyName(ComposerAssembly)).Location;
        var shared = GeneratorReferences.All;
        var present = shared.OfType<PortableExecutableReference>()
            .Any(r => string.Equals(r.FilePath, composer, StringComparison.Ordinal));
        return present ? [.. shared] : [.. shared, MetadataReference.CreateFromFile(composer)];
    }
}

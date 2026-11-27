using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Neemle.OrmLite.Tests.Gates;

// Finds the Composer escapes the generator must not use: a Raw* call, parsing text into a document (Parse, ParseTree)
// or reformatting one (Reformat). What it guarantees: every such call that the generator compiles, in any configuration
// it is built in, is reported, and a name that resolves to something outside Neemle.CSharpComposer is not.
// - Sources are parsed once per configuration with the Generator project's own preprocessor symbols (read from its
//   build, see Tests.csproj), so a call under #if NET, #if DEBUG or #if RELEASE is code, not disabled text. Code that no
//   configuration compiles is not reported.
// - It reads syntax trees, so a comment or line break between receiver and member hides nothing, and it resolves each
//   such name against Neemle.CSharpComposer, so an alias (using C = ...Cs) or a static import (using static ...Cs)
//   reaches the same method and is reported.
// - A name that resolves to anything else (a local Reformat helper, a class of one's own called Cs) is not an escape,
//   and neither is the operand of nameof, which only yields a constant string.
// - Where a name does not resolve at all (a snippet with no using, an unknown or dynamic receiver), it keeps the
//   spelling rule the regex guard had: any invoked Raw* or Reformat, and Parse or ParseTree invoked on a receiver
//   spelled Cs.
// Not covered: a call made through reflection by a string name (ORML-148).
internal static class ComposerEscapeScan
{
    private const string ComposerAssembly = "Neemle.CSharpComposer";

    private const string DefineConstantsKey = "OrmLite.GeneratorDefineConstants.";

    private static readonly Regex RawName = new(@"\ARaw[A-Z][A-Za-z]*\z", RegexOptions.CultureInvariant);

    private static readonly Lazy<MetadataReference[]> References =
        new(BuildReferences, LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<IReadOnlyDictionary<string, string[]>> Configurations =
        new(ReadConfigurations, LazyThreadSafetyMode.ExecutionAndPublication);

    // The preprocessor symbols the Generator project compiles with, per configuration (Release, Debug).
    internal static IReadOnlyDictionary<string, string[]> GeneratorSymbols => Configurations.Value;

    internal static IReadOnlyList<string> Find(IEnumerable<(string Path, string Text)> sources)
    {
        var files = sources.ToArray();
        var hits = new List<string>();
        foreach (var symbols in GeneratorSymbols.Values)
        {
            hits.AddRange(Find(files, symbols));
        }

        return hits.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IEnumerable<string> Find((string Path, string Text)[] sources, string[] symbols)
    {
        var options = new CSharpParseOptions(LanguageVersion.Latest).WithPreprocessorSymbols(symbols);
        var trees = sources.Select(s => CSharpSyntaxTree.ParseText(s.Text, options, s.Path)).ToArray();
        var compilation = CSharpCompilation.Create("ComposerEscapeScan", trees, References.Value,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));

        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var name in tree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>())
            {
                if (IsEscape(name, model))
                {
                    var line = name.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    var shown = (name.Parent is MemberAccessExpressionSyntax access ? access : (SyntaxNode)name).ToString();
                    yield return Path.GetFileName(tree.FilePath) + ":" + line + ": " + shown;
                }
            }
        }
    }

    private static bool IsEscape(SimpleNameSyntax name, SemanticModel model)
    {
        var text = name.Identifier.ValueText;
        var raw = RawName.IsMatch(text) || text == "Reformat";
        var parse = text is "Parse" or "ParseTree";
        if (!raw && !parse) return false;
        if (IsNameofOperand(name, model)) return false;

        var info = model.GetSymbolInfo(name);
        var symbols = info.Symbol is { } symbol ? [symbol] : info.CandidateSymbols;
        if (symbols.Any(s => s is IMethodSymbol && InComposer(s))) return true;
        if (symbols.Any(s => !InComposer(s))) return false;

        var invocation = InvokedThrough(name);
        if (invocation is null) return false;
        if (raw) return true;
        return name.Parent is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "Cs" } };
    }

    private static bool InComposer(ISymbol symbol) => symbol.ContainingAssembly?.Name == ComposerAssembly;

    // True inside the operand of the nameof operator (not of a method someone named nameof): it is never executed.
    private static bool IsNameofOperand(SimpleNameSyntax name, SemanticModel model)
    {
        foreach (var invocation in name.Ancestors().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" }
                && invocation.ArgumentList.Span.Contains(name.Span)
                && model.GetOperation(invocation) is INameOfOperation)
            {
                return true;
            }
        }

        return false;
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

    // Fails closed: a gate that lost the symbols would parse every #if branch as disabled text and pass.
    private static IReadOnlyDictionary<string, string[]> ReadConfigurations()
    {
        var read = typeof(ComposerEscapeScan).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Where(a => a.Key.StartsWith(DefineConstantsKey, StringComparison.Ordinal))
            .ToDictionary(
                a => a.Key[DefineConstantsKey.Length..],
                a => (a.Value ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                StringComparer.Ordinal);
        foreach (var configuration in new[] { "Release", "Debug" })
        {
            if (!read.TryGetValue(configuration, out var symbols) || symbols.Length == 0)
            {
                throw new InvalidOperationException("The Generator project's " + configuration
                    + " preprocessor symbols are missing from the test assembly; see ReadGeneratorDefineConstants in "
                    + "tests/Tests/Tests.csproj.");
            }
        }

        return read;
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

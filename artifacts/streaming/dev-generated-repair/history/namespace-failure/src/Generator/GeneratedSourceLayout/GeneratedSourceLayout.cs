using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Neemle.OrmLite.Generator;

/// <summary>Partitions generated entity declarations without changing their namespace or token order.</summary>
internal static class GeneratedSourceLayout
{
    internal static IEnumerable<(string Hint, string Text)> Entities(string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
        var ns = root.Members.OfType<FileScopedNamespaceDeclarationSyntax>().Single();
        foreach (var type in ns.Members.OfType<TypeDeclarationSyntax>())
        {
            var declaration = GeneratedInitializer.Split(type);
            var unit = root.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(
                ns.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(declaration))));
            yield return ($"OrmLite.Entities.{type.Identifier.ValueText}.g.cs", Wrap(unit.ToFullString()));
        }
    }

    internal static string Wrap(string source) => GeneratedLineWrap.Wrap(source);
}

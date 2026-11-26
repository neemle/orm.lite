using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Neemle.OrmLite.Generator.GeneratedSourceLayout;

/// <summary>Partitions generated entity declarations without changing their namespace or token order.</summary>
internal static class GeneratedSourceLayout
{
    internal static IEnumerable<(string Hint, string Text)> Entities(string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
        var ns = root.Members.OfType<FileScopedNamespaceDeclarationSyntax>().Single();
        var ordinal = 0;
        foreach (var type in ns.Members.OfType<TypeDeclarationSyntax>())
        {
            var unit = root.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(
                ns.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(type))));
            var hint = "OrmLite.Entities." + ordinal.ToString("D4", CultureInfo.InvariantCulture) + ".g.cs";
            ordinal++;
            yield return (hint, Wrap(unit.ToFullString()));
        }
    }

    internal static string Wrap(string source) => GeneratedLineWrap.Wrap(source);
}

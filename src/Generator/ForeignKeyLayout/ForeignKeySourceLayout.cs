using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Neemle.OrmLite.Generator;

/// <summary>Partitions opted-in entity output without changing its declarations or the legacy output path.</summary>
internal static class ForeignKeySourceLayout
{
    internal static void Emit(SourceProductionContext context, string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
        var space = (FileScopedNamespaceDeclarationSyntax)root.Members[0];
        var ordinal = 0;
        foreach (var member in space.Members)
        {
            var isolated = space.WithMembers(SyntaxFactory.SingletonList(member));
            var file = root.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(isolated));
            var formatted = new ForeignKeyLineLayout().Format(file.ToFullString());
            var hint = "OrmLite.Entities." + ordinal.ToString("D4", CultureInfo.InvariantCulture) + ".g.cs";
            ordinal++;
            context.AddSource(hint, formatted);
        }
    }
}

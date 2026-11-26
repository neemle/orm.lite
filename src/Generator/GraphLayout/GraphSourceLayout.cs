using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Neemle.OrmLite.Generator;

/// <summary>Keeps each rendered entity, schema or registration class in its own generated source file.</summary>
internal static class GraphSourceLayout
{
    internal static void Emit(SourceProductionContext context, string source)
    {
        var document = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
        var space = (FileScopedNamespaceDeclarationSyntax)document.Members[0];
        var ordinal = 0;
        foreach (var declaration in space.Members)
        {
            var isolated = space.WithMembers(SyntaxFactory.SingletonList(declaration));
            var file = document.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(isolated));
            var hint = "OrmLite.Entities." + ordinal.ToString("D4", CultureInfo.InvariantCulture) + ".g.cs";
            context.AddSource(hint, Wrap(file));
            ordinal++;
        }
    }

    private static string Wrap(CompilationUnitSyntax file)
    {
        var originalMap = file.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .SingleOrDefault(method => method.Identifier.ValueText == "Map");
        var wrapped = CSharpSyntaxTree.ParseText(GraphEmitter.WrapMembers(file.ToFullString()))
            .GetCompilationUnitRoot();
        if (originalMap is null) return wrapped.ToFullString();
        var formattedMap = wrapped.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Single(method => method.Identifier.ValueText == "Map");
        return wrapped.ReplaceNode(formattedMap, originalMap).ToFullString();
    }
}

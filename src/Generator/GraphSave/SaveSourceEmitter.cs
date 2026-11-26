using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;

namespace Neemle.OrmLite.Generator.GraphSave;

internal static class SaveSourceEmitter
{
    internal static string Augment(string source, ImmutableArray<Entity> entities)
    {
        var supported = entities.Where(SaveBindingEmitter.Supported).ToImmutableArray();
        if (supported.IsEmpty) return source;
        var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
        var types = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Where(c => supported.Any(e => c.Identifier.ValueText == e.Type + "Orm")).ToArray();
        root = root.ReplaceNodes(types, (type, _) => type.AddMembers(SaveBindingEmitter.Members(
            supported.Single(e => type.Identifier.ValueText == e.Type + "Orm"), entities).Select(Parse).ToArray()));
        return Initialize(root, supported).ToFullString();
    }

    private static CompilationUnitSyntax Initialize(CompilationUnitSyntax root, ImmutableArray<Entity> entities)
    {
        var owner = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(c => c.Identifier.ValueText == "OrmMetaInit");
        var init = owner.Members.OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == "Init");
        var groups = entities.Chunk(20).ToArray();
        var calls = groups.Select((_, index) => SyntaxFactory.ParseStatement("RegisterSave" + index + "();"));
        var modified = owner.ReplaceNode(init, init.WithBody(init.Body!.AddStatements(calls.ToArray())));
        foreach (var (group, index) in groups.Select((group, index) => (group, index)))
            modified = modified.AddMembers(Parse("private static void RegisterSave" + index + "() { "
                + string.Join(" ", group.Select(e => e.Type + "Orm.RegisterSave();")) + " }"));
        return root.ReplaceNode(owner, modified);
    }

    private static MemberDeclarationSyntax Parse(string source) => SyntaxFactory.ParseMemberDeclaration(source)!
        .NormalizeWhitespace().WithLeadingTrivia(SyntaxFactory.LineFeed).WithTrailingTrivia(SyntaxFactory.LineFeed);
}

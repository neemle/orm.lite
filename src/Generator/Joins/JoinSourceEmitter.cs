using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;

namespace Neemle.OrmLite.Generator.Joins;

internal static class JoinSourceEmitter
{
    internal static string Augment(string source, ImmutableArray<Entity> entities)
    {
        var calls = entities.Where(JoinCodecEmitter.Supported).SelectMany(JoinCodecEmitter.Statements).ToArray();
        if (calls.Length == 0) return source;
        var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
        var owner = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(c => c.Identifier.ValueText == "OrmMetaInit");
        var init = owner.Members.OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == "Init");
        var groups = calls.Chunk(4).ToArray();
        var starts = groups.Select((_, i) => SyntaxFactory.ParseStatement("RegisterJoin" + i + "();"));
        var modified = owner.ReplaceNode(init, init.WithBody(init.Body!.AddStatements(starts.ToArray())));
        foreach (var (group, index) in groups.Select((group, index) => (group, index)))
            modified = modified.AddMembers(SyntaxFactory.ParseMemberDeclaration(
                "private static void RegisterJoin" + index + "() { " + string.Join(" ", group) + " }")!
                .NormalizeWhitespace().WithLeadingTrivia(SyntaxFactory.LineFeed)
                .WithTrailingTrivia(SyntaxFactory.LineFeed));
        return root.ReplaceNode(owner, modified).ToFullString();
    }
}

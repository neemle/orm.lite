using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;

namespace Neemle.OrmLite.Generator.Orphans;

internal sealed class OrphanBootstrapEmitter
{
    private ClassDeclarationSyntax _owner;
    private readonly HashSet<string> _used;
    private readonly Dictionary<string, string> _factories = new();
    private readonly List<StatementSyntax> _calls = new();

    internal OrphanBootstrapEmitter(ClassDeclarationSyntax owner)
    {
        _owner = owner;
        _used = owner.DescendantTokens().Where(t => t.IsKind(SyntaxKind.IdentifierToken))
            .Select(t => t.ValueText).ToHashSet();
    }

    internal ClassDeclarationSyntax Emit(ImmutableArray<Entity> entities,
        HashSet<(string Entity, string Nav)> blocked)
    {
        foreach (var entity in entities)
        {
            var factory = Reserve("BindOrphan" + _factories.Count);
            _factories.Add(entity.Full, factory);
            _owner = _owner.AddMembers(Parse(OrphanBindingSource.Factory(entity, factory)));
        }
        AddPhases(_factories.Values.Select(OrphanBindingSource.EntityRegistration), "RegisterOrphanEntities");
        AddPhases(OrphanCollectionReader.All(entities, blocked).Select(c =>
            OrphanBindingSource.Collection(c, _factories[c.Parent.Full], _factories[c.Child.Full])),
            "RegisterOrphanCollections");
        var init = _owner.Members.OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == "Init");
        return Initialize(init);
    }

    private ClassDeclarationSyntax Initialize(MethodDeclarationSyntax init)
    {
        var statements = init.Body!.Statements.Concat(_calls).ToArray();
        if (statements.Length <= 20)
            return _owner.ReplaceNode(init, init.WithBody(SyntaxFactory.Block(statements)));
        var members = new List<MemberDeclarationSyntax>();
        var calls = new List<StatementSyntax>();
        foreach (var group in statements.Chunk(20))
        {
            var name = Reserve("InitOrphanPhase" + calls.Count);
            members.Add(Parse("private static void " + name + "() { "
                + string.Join(" ", group.Select(s => s.ToFullString())) + " }"));
            calls.Add(SyntaxFactory.ParseStatement(name + "();"));
        }
        return _owner.ReplaceNode(init, init.WithBody(SyntaxFactory.Block(calls))).AddMembers(members.ToArray());
    }

    private void AddPhases(IEnumerable<string> statements, string prefix)
    {
        foreach (var group in statements.Chunk(20))
        {
            var name = Reserve(prefix + _calls.Count);
            _owner = _owner.AddMembers(Parse("private static void " + name + "() { "
                + string.Join(" ", group) + " }"));
            _calls.Add(SyntaxFactory.ParseStatement(name + "();"));
        }
    }

    private string Reserve(string name)
    {
        while (!_used.Add(name)) name += "_";
        return name;
    }

    private static MemberDeclarationSyntax Parse(string text) => SyntaxFactory.ParseMemberDeclaration(text)!
        .NormalizeWhitespace().WithLeadingTrivia(SyntaxFactory.LineFeed).WithTrailingTrivia(SyntaxFactory.LineFeed);
}

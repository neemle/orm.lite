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
        if (_calls.Count == 0) return _owner;
        var statements = init.Body!.Statements.Concat(_calls).ToArray();
        if (statements.Length <= 20) return _owner.ReplaceNode(init, WithStatements(init, statements));
        var members = new List<MemberDeclarationSyntax>();
        var calls = new List<StatementSyntax>();
        foreach (var group in statements.Chunk(20))
        {
            var name = Reserve("InitOrphanPhase" + calls.Count);
            members.Add(Parse("private static void " + name + "() { "
                + string.Join(" ", group.Select(s => s.ToFullString())) + " }"));
            calls.Add(Call(name));
        }
        return _owner.ReplaceNode(init, WithStatements(init, calls)).AddMembers(members.ToArray());
    }

    // Keeps the braces of Init with their layout and puts every appended call on its own line.
    private static MethodDeclarationSyntax WithStatements(MethodDeclarationSyntax init,
        IReadOnlyList<StatementSyntax> statements)
    {
        var list = statements.Select(s => s.WithTrailingTrivia(
            s.GetTrailingTrivia().Where(t => !t.IsKind(SyntaxKind.WhitespaceTrivia)))).ToList();
        if (list.Count != 0) list[list.Count - 1] = list[list.Count - 1].WithTrailingTrivia(SyntaxFactory.LineFeed);
        var body = init.Body!.WithStatements(SyntaxFactory.List(list));
        return init.WithBody(body.WithCloseBraceToken(
            body.CloseBraceToken.WithLeadingTrivia(SyntaxFactory.Whitespace("    "))));
    }

    private static StatementSyntax Call(string name) => SyntaxFactory.ParseStatement(name + "();")
        .WithLeadingTrivia(SyntaxFactory.LineFeed, SyntaxFactory.Whitespace("        "));

    private void AddPhases(IEnumerable<string> statements, string prefix)
    {
        foreach (var group in statements.Chunk(20))
        {
            var name = Reserve(prefix + _calls.Count);
            _owner = _owner.AddMembers(Parse("private static void " + name + "() { "
                + string.Join(" ", group) + " }"));
            _calls.Add(Call(name));
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

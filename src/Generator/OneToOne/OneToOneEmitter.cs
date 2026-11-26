using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Neemle.CSharpComposer;
using Neemle.CSharpComposer.Model;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;

namespace Neemle.OrmLite.Generator;

internal static class OneToOneEmitter
{
    private const string Runtime = "global::Neemle.OrmLite.";

    internal static CsExpression Describe(CsExpression descriptor, Entity entity)
    {
        foreach (var p in entity.OneToOnePairs.Where(p => p.OwnerFull == entity.Full))
        {
            var owner = New("OneToOneOwner", Syn.Str(p.OwnerFull), Syn.Str(p.Intent.ForeignKey),
                Syn.Str(p.ForeignKeyColumn));
            var target = New("OneToOneTarget", Syn.Str(p.PrincipalFull), Syn.Str(p.PrincipalKeyColumn),
                Syn.Str(p.Intent.Navigation), Syn.Str(p.Intent.Inverse));
            var constraints = New("OneToOneConstraints", Syn.Str(p.ForeignKeyName), Syn.Str(p.Intent.UniqueName),
                Cs.Id("Nodes").Dot("ReferentialAction").Dot(Action(p.Action)));
            descriptor = descriptor.Dot("OneToOne").Of(New("OneToOneDescriptor", owner, target, constraints));
        }
        return descriptor;
    }

    private static CsExpression New(string type, params CsExpression[] values) =>
        new(ExpressionKind.New, "", new Values<CsExpression>(values)) { Type = Syn.Lite(type) };

    private static string Action(int action) => action switch
    { 0 => "NoAction", 1 => "Restrict", 2 => "Cascade", _ => "SetNull" };

    internal static string Augment(string source, ImmutableArray<Entity> entities)
    {
        if (!entities.Any(e => !e.OneToOnePairs.IsEmpty)) return source;
        var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
        var space = root.Members.OfType<FileScopedNamespaceDeclarationSyntax>().Single();
        var members = space.Members.Select(m => m is ClassDeclarationSyntax type
            ? AugmentType(type, entities) : m);
        return root.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(space.WithMembers(
            SyntaxFactory.List(members)))).NormalizeWhitespace().ToFullString();
    }

    private static ClassDeclarationSyntax AugmentType(ClassDeclarationSyntax type, ImmutableArray<Entity> entities)
    {
        if (type.Identifier.ValueText == "OrmMetaInit") return Register(type, entities);
        var entity = entities.FirstOrDefault(e => type.Identifier.ValueText == e.Type + "Orm");
        if (entity is null) return type;
        foreach (var pair in entity.OneToOnePairs)
            foreach (var unit in new[] { false, true })
                type = type.AddMembers(SyntaxFactory.ParseMemberDeclaration(
                    new OneToOneLoader(entity, pair, unit).Source())!);
        return type;
    }

    private static ClassDeclarationSyntax Register(ClassDeclarationSyntax type, ImmutableArray<Entity> entities)
    {
        var init = type.Members.OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == "Init");
        var statements = init.Body!.Statements.AddRange(entities.Where(e => !e.OneToOnePairs.IsEmpty).Select(e =>
            SyntaxFactory.ParseStatement(Runtime + "OneToOneReadGuard.Register<"
                + e.Full + ">(" + e.Type + "Orm.Map);")));
        if (statements.Count <= 20) return type.ReplaceNode(init, init.WithBody(SyntaxFactory.Block(statements)));
        var groups = statements.Chunk(20).ToArray();
        var calls = groups.Select((_, i) => SyntaxFactory.ParseStatement("InitPhase" + i + "();"));
        type = type.ReplaceNode(init, init.WithBody(SyntaxFactory.Block(calls)));
        foreach (var (group, i) in groups.Select((g, i) => (g, i)))
            type = type.AddMembers(SyntaxFactory.ParseMemberDeclaration("private static void InitPhase" + i
                + "() { " + string.Join("\n", group.Select(s => s.ToFullString())) + " }")!);
        return type;
    }

}
